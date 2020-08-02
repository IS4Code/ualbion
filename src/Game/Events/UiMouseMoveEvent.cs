﻿using UAlbion.Api;

namespace UAlbion.Game.Events
{
    [Event("ui_mouse_move")]
    public class UiMouseMoveEvent : Event, IVerboseEvent
    {
        public UiMouseMoveEvent(int x, int y)
        {
            X = x;
            Y = y;
        }

        [EventPart("x")] public int X { get; }
        [EventPart("y")] public int Y { get; }
    }
}