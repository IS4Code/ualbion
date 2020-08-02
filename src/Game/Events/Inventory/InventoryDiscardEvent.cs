﻿using UAlbion.Api;
using UAlbion.Formats.Assets;

namespace UAlbion.Game.Events.Inventory
{
    [Event("inv:discard")]
    public class InventoryDiscardEvent : InventorySlotEvent
    {
        [EventPart("norm_x")] public float NormX { get; }
        [EventPart("norm_y")] public float NormY { get; }

        public InventoryDiscardEvent(float normX, float normY, InventoryType inventoryType, ushort id, ItemSlotId slotId)
            : base(inventoryType, id, slotId)
        {
            NormX = normX;
            NormY = normY;
        }
    }
}