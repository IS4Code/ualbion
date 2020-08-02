﻿using System;
using System.Linq;
using System.Numerics;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets;
using UAlbion.Formats.Config;
using UAlbion.Game.Events;
using UAlbion.Game.Events.Inventory;
using UAlbion.Game.Events.Transitions;
using UAlbion.Game.Input;
using UAlbion.Game.Text;

namespace UAlbion.Game.State.Player
{
    public class InventoryEventManager : Component
    {
    }

    public class InventoryManager : ServiceComponent<IInventoryManager>, IInventoryManager
    {
        readonly Func<InventoryId, Inventory> _getInventory;
        readonly ItemSlot _hand = new ItemSlot(new InventorySlotId(InventoryType.Temporary, 0, ItemSlotId.None));
        IEvent _returnItemInHandEvent;

        ItemSlot GetSlot(InventorySlotId id) => _getInventory(id.Inventory)?.GetSlot(id.Slot);
        public ReadOnlyItemSlot ItemInHand { get; }
        public InventoryManager(Func<InventoryId, Inventory> getInventory)
        {
            On<InventoryReturnItemInHandEvent>(_ => ReturnItemInHand());
            On<InventoryDestroyItemInHandEvent>(_ =>
            {
                if (_hand.Amount > 0)
                    _hand.Amount--;
                ReturnItemInHand();
            });
            OnAsync<InventorySwapEvent>(OnSlotEvent);
            OnAsync<InventoryPickupEvent>(OnSlotEvent);
            OnAsync<InventoryGiveItemEvent>(OnGiveItem);
            OnAsync<InventoryDiscardEvent>(OnDiscard);
            On<SetInventorySlotUiPositionEvent>(OnSetSlotUiPosition);
            _getInventory = getInventory;
            ItemInHand = new ReadOnlyItemSlot(_hand);
        }

        void ReturnItemInHand()
        {
            if (_returnItemInHandEvent == null || _hand.Item == null)
                return;

            Receive(_returnItemInHandEvent, null);
        }

        void OnSetSlotUiPosition(SetInventorySlotUiPositionEvent e)
        {
            var inventory = _getInventory(e.InventorySlotId.Inventory);
            inventory.SetSlotUiPosition(e.Slot, new Vector2(e.X, e.Y));
        }

        public InventoryAction GetInventoryAction(InventorySlotId slotId)
        {
            var slot = GetSlot(slotId);
            if (slot == null || slot.Id.Slot == ItemSlotId.None)
                return InventoryAction.Nothing;

            switch (_hand.Item, slot.Item)
            {
                case (null, null): return InventoryAction.Nothing;
                case (null, _): return InventoryAction.Pickup;

                case (Gold _, Gold _):
                case (Rations _, Rations _): return InventoryAction.Coalesce;
                case (Gold _, null): return slotId.Slot == ItemSlotId.Gold ? InventoryAction.PutDown : InventoryAction.Nothing;
                case (Rations _, null): return slotId.Slot == ItemSlotId.Rations ? InventoryAction.PutDown : InventoryAction.Nothing;

                case (ItemData _, null): return InventoryAction.PutDown;
                case (ItemData _, ItemData _) when slot.CanCoalesce(_hand):
                    return slot.Amount >= ItemSlot.MaxItemCount
                        ? InventoryAction.NoCoalesceFullStack
                        : InventoryAction.Coalesce;

                case (ItemData _, ItemData _):
                    return InventoryAction.Swap;

                default:
                    return InventoryAction.Nothing;
            }
        }

        void Update(InventoryId id) => Raise(new InventoryChangedEvent(id.Type, id.Id));

        void PickupItem(ItemSlot slot, ushort? quantity)
        {
            if (!CanItemBeTaken(slot))
                return; // TODO: Message

            _hand.TransferFrom(slot, quantity);
            _returnItemInHandEvent = new InventorySwapEvent(slot.Id.Type, slot.Id.Id, slot.Id.Slot);
        }

        static bool DoesSlotAcceptItem(ICharacterSheet sheet, ItemSlotId slotId, ItemData item)
        {
            switch (sheet.Gender)
            {
                case Gender.Male: if (!item.AllowedGender.HasFlag(GenderMask.Male)) return false; break;
                case Gender.Female: if (!item.AllowedGender.HasFlag(GenderMask.Female)) return false; break;
                case Gender.Neuter: if (!item.AllowedGender.HasFlag(GenderMask.Neutral)) return false; break;
            }

            if (!item.Class.IsAllowed(sheet.Class))
                return false;

            // if (!item.Race.IsAllowed(sheet.Race)) // Apparently never implemented in original game?
            //     return false;

            if (item.SlotType != slotId)
                return false;

            switch (slotId)
            {
                case ItemSlotId.LeftHand:
                    {
                        return !(sheet.Inventory.RightHand.Item is ItemData rightHandItem) || rightHandItem.Hands <= 1;
                    }
                case ItemSlotId.Tail:
                    return
                        item.TypeId == ItemType.CloseRangeWeapon
                        && (item.SlotType == ItemSlotId.Tail || item.SlotType == ItemSlotId.RightHandOrTail);
                default:
                    return true;
            }
        }

        bool DoesSlotAcceptItemInHand(InventoryType type, int id, ItemSlotId slotId)
        {
            switch (_hand?.Item)
            {
                case null: return true;
                case Gold _: return slotId == ItemSlotId.Gold;
                case Rations _: return slotId == ItemSlotId.Rations;
                case ItemData _ when slotId < ItemSlotId.NormalSlotCount: return true;
                case ItemData _ when type != InventoryType.Player: return false;
                case ItemData item:
                    {
                        var state = Resolve<IGameState>();
                        var sheet = state.GetPartyMember((PartyCharacterId)id);
                        return DoesSlotAcceptItem(sheet, slotId, item);
                    }

                default:
                    throw new InvalidOperationException($"Unexpected item type in hand: {_hand.GetType()}");
            }
        }

        ItemSlotId GetBestSlot(InventorySlotId id)
        {
            if (_hand.Item is Gold) return ItemSlotId.Gold;
            if (_hand.Item is Rations) return ItemSlotId.Rations;
            if (!(_hand.Item is ItemData item)) return id.Slot; // Shouldn't be possible

            if (id.Type != InventoryType.Player || !id.Slot.IsBodyPart())
                return ItemSlotId.None;

            var state = Resolve<IGameState>();
            var sheet = state.GetPartyMember((PartyCharacterId)id.Id);
            if (DoesSlotAcceptItem(sheet, id.Slot, item)) return id.Slot;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.Head, item)) return ItemSlotId.Head;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.Neck, item)) return ItemSlotId.Neck;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.Tail, item)) return ItemSlotId.Tail;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.RightHand, item)) return ItemSlotId.RightHand;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.LeftHand, item)) return ItemSlotId.LeftHand;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.Chest, item)) return ItemSlotId.Chest;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.RightFinger, item)) return ItemSlotId.RightFinger;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.LeftFinger, item)) return ItemSlotId.LeftFinger;
            if (DoesSlotAcceptItem(sheet, ItemSlotId.Feet, item)) return ItemSlotId.Feet;

            return ItemSlotId.None;
        }

        static bool CanItemBeTaken(ItemSlot slot)
        {
            // TODO: Goddess' amulet etc
            switch (slot.Item)
            {
                case Gold _:
                case Rations _: return true;
                case ItemData item when
                    slot.Id.Slot < ItemSlotId.Slot0 &&
                    item.Flags.HasFlag(ItemSlotFlags.Cursed):
                    return false;
                default: return true;
            }
        }

        bool OnGiveItem(InventoryGiveItemEvent e, Action continuation)
        {
            var inventory = _getInventory((InventoryId)e.MemberId);
            switch (_hand.Item)
            {
                case Gold _: inventory.Gold.TransferFrom(_hand, null); break;
                case Rations _: inventory.Rations.TransferFrom(_hand, null); break;
                case ItemData item:
                    {
                        ItemSlot slot = null;
                        if (item.IsStackable)
                        {
                            slot = inventory.BackpackSlots.FirstOrDefault(x =>
                                x.Item is ItemData existing &&
                                existing.Id == item.Id);
                        }

                        slot ??= inventory.BackpackSlots.FirstOrDefault(x => x.Item == null);
                        slot?.TransferFrom(_hand, null);

                        break;
                    }
                default: return false; // Unknown or null
            }

            Update(inventory.Id);
            Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
            continuation();
            return true;
        }

        void GetQuantity(bool discard, IInventory inventory, ItemSlotId slotId, Action<int> continuation)
        {
            var slot = inventory.GetSlot(slotId);
            if (slot.Amount == 1)
            {
                continuation(1);
                return;
            }

            var (maxQuantity, text, icon) = slot.Item switch
            {
                Gold _ => (
                    slot.Amount,
                    discard
                        ? SystemTextId.Gold_ThrowHowMuchGoldAway
                        : SystemTextId.Gold_TakeHowMuchGold,
                    CoreSpriteId.UiGold.ToAssetId()),

                Rations _ => (
                    slot.Amount,
                    discard
                        ? SystemTextId.Gold_ThrowHowManyRationsAway
                        : SystemTextId.Gold_TakeHowManyRations,
                    CoreSpriteId.UiFood.ToAssetId()),

                ItemData item => (
                    slot.Amount,
                    discard
                        ? SystemTextId.InvMsg_ThrowHowManyItemsAway
                        : SystemTextId.InvMsg_TakeHowManyItems,
                    item.Icon.ToAssetId()
                ),
                { } x => throw new InvalidOperationException($"Unexpected item contents {x}")
            };

            if (RaiseAsync(new ItemQuantityPromptEvent(text, icon, maxQuantity, slotId == ItemSlotId.Gold), continuation) == 0)
            {
                ApiUtil.Assert("ItemManager.GetQuantity tried to open a quantity dialog, but no-one was listening for the event.");
                continuation(0);
            }
        }

        bool OnSlotEvent(InventorySlotEvent e, Action continuation)
        {
            var slotId = new InventorySlotId(e.InventoryType, e.InventoryId, e.SlotId);
            bool redirected = false;
            bool complete = false;

            if (!DoesSlotAcceptItemInHand(e.InventoryType, e.InventoryId, e.SlotId))
            {
                slotId = new InventorySlotId(slotId.Type, slotId.Id, GetBestSlot(slotId));
                redirected = true;
            }

            if (slotId.Slot == ItemSlotId.None || slotId.Slot == ItemSlotId.CharacterBody)
                return false;

            Inventory inventory = _getInventory(slotId.Inventory);
            ItemSlot slot = inventory?.GetSlot(slotId.Slot);
            if (slot == null)
                return false;

            var config = Resolve<GameConfig>();
            var cursorManager = Resolve<ICursorManager>();
            var window = Resolve<IWindowManager>();
            var cursorUiPosition = window.PixelToUi(cursorManager.Position);

            switch (GetInventoryAction(slotId))
            {
                case InventoryAction.Pickup:
                {
                    if (slot.Amount == 1)
                    {
                        PickupItem(slot, null);
                        complete = true;
                    }
                    else if (e is InventoryPickupEvent pickup)
                    {
                        PickupItem(slot, pickup.Amount);
                        complete = true;
                    }
                    else
                    {
                        GetQuantity(false, inventory, e.SlotId, quantity =>
                        {
                            if (quantity > 0)
                                PickupItem(slot, (ushort)quantity);

                            Update(slotId.Inventory);
                            Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
                            continuation();
                        });
                    }
                    break;
                }
                case InventoryAction.PutDown:
                {
                    if (redirected)
                    {
                        var transitionEvent = new LinearItemTransitionEvent(
                            _hand.ItemId ?? ItemId.Knife,
                            (int)cursorUiPosition.X,
                            (int)cursorUiPosition.Y,
                            (int)slot.LastUiPosition.X,
                            (int)slot.LastUiPosition.Y,
                            config.UI.Transitions.ItemMovementTransitionTimeSeconds);

                        ItemSlot temp = new ItemSlot(new InventorySlotId(InventoryType.Temporary, 0, 0));
                        temp.TransferFrom(_hand, null);
                        Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
                        RaiseAsync(transitionEvent, () =>
                        {
                            slot.TransferFrom(temp, null);
                            Update(slotId.Inventory);
                            continuation();
                        });
                    }
                    else
                    {
                        slot.TransferFrom(_hand, null);
                        complete = true;
                    }
                    break;
                }
                case InventoryAction.Swap:
                {
                    if (redirected)
                    {
                        // Original game didn't handle this, but doesn't hurt.
                        var transitionEvent1 = new LinearItemTransitionEvent(
                            _hand.ItemId ?? ItemId.Knife,
                            (int)cursorUiPosition.X,
                            (int)cursorUiPosition.Y,
                            (int)slot.LastUiPosition.X,
                            (int)slot.LastUiPosition.Y,
                            config.UI.Transitions.ItemMovementTransitionTimeSeconds);

                        var transitionEvent2 = new LinearItemTransitionEvent(
                            slot.ItemId ?? ItemId.Knife,
                            (int)slot.LastUiPosition.X,
                            (int)slot.LastUiPosition.Y,
                            (int)cursorUiPosition.X,
                            (int)cursorUiPosition.Y,
                            config.UI.Transitions.ItemMovementTransitionTimeSeconds);

                        ItemSlot temp1 = new ItemSlot(new InventorySlotId(InventoryType.Temporary, 0, 0));
                        ItemSlot temp2 = new ItemSlot(new InventorySlotId(InventoryType.Temporary, 0, 0));
                        temp1.TransferFrom(_hand, null);
                        temp2.TransferFrom(slot, null);

                        RaiseAsync(transitionEvent1, () =>
                        {
                            slot.TransferFrom(temp1, null);
                            Update(slotId.Inventory);
                            Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
                            continuation();
                        });

                        RaiseAsync(transitionEvent2, () =>
                        {
                            _hand.TransferFrom(temp2, null);
                            _returnItemInHandEvent = new InventorySwapEvent(slot.Id.Type, slot.Id.Id, slot.Id.Slot);
                            Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
                        });

                        Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
                    }
                    else
                    {
                        SwapItems(slot);
                        complete = true;
                    }
                    break;
                }

                // Shouldn't be possible for this to be a redirect as redirects only happen between body parts and they don't allow stacks.
                case InventoryAction.Coalesce:
                {
                    if (e is InventoryPickupEvent pickup)
                        PickupItem(slot, pickup.Amount);
                    else
                        CoalesceItems(slot);
                    complete = true;
                    break;
                }
                case InventoryAction.NoCoalesceFullStack: complete = true; break; // No-op
            }

            if (complete)
            {
                continuation();
                Update(slotId.Inventory);
                Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
            }

            return true;
        }

        bool OnDiscard(InventoryDiscardEvent e, Action continuation)
        {
            var inventoryId = new InventoryId(e.InventoryType, e.InventoryId);
            var inventory = _getInventory(inventoryId);
            GetQuantity(true, inventory, e.SlotId, quantity =>
            {
                if (quantity <= 0)
                {
                    continuation();
                    return;
                }

                var slot = inventory.GetSlot(e.SlotId);
                ushort itemsToDrop = Math.Min((ushort)quantity, slot.Amount);

                var prompt = slot.Item switch
                {
                    Gold _ => SystemTextId.Gold_ReallyThrowTheGoldAway,
                    Rations _ => SystemTextId.Gold_ReallyThrowTheRationsAway,
                    ItemData _ when itemsToDrop == 1 => SystemTextId.InvMsg_ReallyThrowThisItemAway,
                    _ => SystemTextId.InvMsg_ReallyThrowTheseItemsAway,
                };

                RaiseAsync(new YesNoPromptEvent(prompt), response =>
                {
                    if (!response)
                    {
                        continuation();
                        return;
                    }

                    if (slot.ItemId.HasValue)
                    {
                        var config = Resolve<GameConfig>();
                        for (int i = 0; i < itemsToDrop && i < config.UI.Transitions.MaxDiscardTransitions; i++)
                            Raise(new GravityItemTransitionEvent(slot.ItemId.Value, e.NormX, e.NormY));
                    }

                    slot.Amount -= itemsToDrop;
                    Update(inventoryId);
                    Raise(new SetCursorEvent(_hand.Item == null ? CoreSpriteId.Cursor : CoreSpriteId.CursorSmall));
                    continuation();
                });
            });
            return true;
        }

        void CoalesceItems(ItemSlot slot)
        {
            ApiUtil.Assert(slot.CanCoalesce(_hand));
            ApiUtil.Assert(slot.Amount < ItemSlot.MaxItemCount || slot.Item is Gold || slot.Item is Rations);
            slot.TransferFrom(_hand, null);
        }

        void SwapItems(ItemSlot slot)
        {
            // Check if the item can be taken
            if (!CanItemBeTaken(slot))
                return; // TODO: Message

            _hand.Swap(slot);
            _returnItemInHandEvent = new InventorySwapEvent(slot.Id.Type, slot.Id.Id, slot.Id.Slot);
        }

        void RaiseStatusMessage(SystemTextId textId)
            => Raise(new DescriptionTextEvent(Resolve<ITextFormatter>().Format(textId)));

        public int GetItemCount(InventoryId id, ItemId item) => _getInventory(id).EnumerateAll().Where(x => x.ItemId == item).Sum(x => (int?)x.Amount) ?? 0;
        public ushort TryGiveItems(InventoryId id, ItemSlot donor, ushort? amount)
        {
            // TODO: Ensure weight limit is not exceeded?
            ushort totalTransferred = 0;
            ushort remaining = amount ?? ushort.MaxValue;
            var inventory = _getInventory(id);

            if (donor.ItemId == ItemId.Gold)
                return inventory.Gold.TransferFrom(donor, remaining);

            if (donor.ItemId == ItemId.Rations)
                return inventory.Rations.TransferFrom(donor, remaining);

            for (int i = 0; i < (int)ItemSlotId.NormalSlotCount && amount != 0; i++)
            {
                if (!inventory.Slots[i].CanCoalesce(donor))
                    continue;

                ushort transferred = inventory.Slots[i].TransferFrom(donor, remaining);
                remaining -= transferred;
                totalTransferred += transferred;
                if (remaining == 0 || donor.Item == null)
                    break;
            }

            return totalTransferred;
        }

        public ushort TryTakeItems(InventoryId id, ItemSlot acceptor, ItemId item, ushort? amount)
        {
            ushort totalTransferred = 0;
            ushort remaining = amount ?? ushort.MaxValue;
            var inventory = _getInventory(id);

            if (item == ItemId.Gold)
                return acceptor.TransferFrom(inventory.Gold, remaining);

            if (item == ItemId.Rations)
                return acceptor.TransferFrom(inventory.Rations, remaining);

            for (int i = 0; i < (int)ItemSlotId.NormalSlotCount && amount != 0; i++)
            {
                if (inventory.Slots[i].ItemId != item) 
                    continue;

                ushort transferred = acceptor.TransferFrom(inventory.Slots[i], remaining);
                totalTransferred += transferred;
                remaining -= transferred;
                if (remaining == 0)
                    break;
            }

            return totalTransferred;
        }
    }
}
