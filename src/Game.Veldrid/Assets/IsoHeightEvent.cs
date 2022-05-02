﻿using UAlbion.Api;
using UAlbion.Api.Eventing;

namespace UAlbion.Game.Veldrid.Assets;

[Event("iso_height")]
public class IsoHeightEvent : Event
{
    public IsoHeightEvent(int delta) => Delta = delta;
    [EventPart("delta")] public int Delta { get; }
}