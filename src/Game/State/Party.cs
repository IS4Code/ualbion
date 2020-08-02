﻿using System;
using System.Collections.Generic;
using System.Linq;
using UAlbion.Core;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets;
using UAlbion.Formats.MapEvents;
using UAlbion.Game.Events;
using UAlbion.Game.Text;

namespace UAlbion.Game.State
{
    public class Party : ServiceComponent<IParty>, IParty
    {
        public const int MaxPartySize = 6;

        readonly IDictionary<PartyCharacterId, CharacterSheet> _characterSheets;
        readonly List<Player.Player> _statusBarOrder = new List<Player.Player>();
        readonly List<Player.Player> _walkOrder = new List<Player.Player>();
        readonly IReadOnlyList<Player.Player> _readOnlyStatusBarOrder;
        readonly IReadOnlyList<Player.Player> _readOnlyWalkOrder;

        public Party(
            IDictionary<PartyCharacterId, CharacterSheet> characterSheets,
            IList<PartyCharacterId?> statusBarOrder,
            Func<InventoryId, Inventory> getInventory)
        {
            On<AddPartyMemberEvent>(e => SetLastResult(AddMember(e.PartyMemberId)));
            On<RemovePartyMemberEvent>(e => SetLastResult(RemoveMember(e.PartyMemberId)));
            On<SetPartyLeaderEvent>(e =>
            {
                Leader = e.PartyMemberId;
                Raise(new SetContextEvent(ContextType.Leader, AssetType.PartyMember, (int)e.PartyMemberId));
                Raise(e);
            });

            _characterSheets = characterSheets;
            _readOnlyStatusBarOrder = _statusBarOrder.AsReadOnly();
            _readOnlyWalkOrder = _walkOrder.AsReadOnly();
            AttachChild(new PartyInventory(getInventory));

            foreach (var member in statusBarOrder)
                if (member.HasValue)
                    AddMember(member.Value);
        }

        public IPlayer this[PartyCharacterId id] => _statusBarOrder.FirstOrDefault(x => x.Id == id);
        public IReadOnlyList<IPlayer> StatusBarOrder => _readOnlyStatusBarOrder;
        public IReadOnlyList<IPlayer> WalkOrder => _readOnlyWalkOrder;
        public int TotalGold => _statusBarOrder.Sum(x => x.Effective.Inventory.Gold.Amount);
        public int GetItemCount(ItemId itemId) =>
            _statusBarOrder
            .SelectMany(x => x.Effective.Inventory.EnumerateAll())
            .Where(x => x.Item is ItemData item && item.Id == itemId)
            .Sum(x => x.Amount);

        // The current party leader (shown with a white outline on
        // health bar and slightly raised in the status bar)
        public PartyCharacterId Leader
        {
            get => _walkOrder[0].Id;
            private set
            {
                int index = _walkOrder.FindIndex(x => x.Id == value);
                if (index == -1)
                    return;

                var player = _walkOrder[index];
                _walkOrder.RemoveAt(index);
                _walkOrder.Insert(0, player);
            }
        }

        bool AddMember(PartyCharacterId id)
        {
            bool InsertMember(Player.Player newPlayer)
            {
                for (int i = 0; i < MaxPartySize; i++)
                {
                    if (_statusBarOrder.Count == i || _statusBarOrder[i].Id > id)
                    {
                        _statusBarOrder.Insert(i, newPlayer);
                        return true;
                    }
                }
                return false;
            }

            if (_statusBarOrder.Any(x => x.Id == id))
                return false;

            var player = new Player.Player(id, _characterSheets[id]);
            if (!InsertMember(player)) 
                return false;

            _walkOrder.Add(player);
            AttachChild(player);
            Raise(new PartyChangedEvent());
            return true;
        }

        bool RemoveMember(PartyCharacterId id)
        {
            var player = _statusBarOrder.FirstOrDefault(x => x.Id == id);
            if (player == null)
                return false;

            _walkOrder.Remove(player);
            _statusBarOrder.Remove(player);
            player.Remove();
            Raise(new PartyChangedEvent());
            return true;
        }

        void SetLastResult(bool result) => Resolve<IEventManager>().LastEventResult = result;

        public void Clear()
        {
            foreach(var id in _statusBarOrder.Select(x => x.Id).ToList())
                RemoveMember(id);
        }
    }
}

