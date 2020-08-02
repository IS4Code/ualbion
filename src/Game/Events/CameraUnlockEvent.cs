﻿using UAlbion.Api;

namespace UAlbion.Game.Events
{
    [Event("camera_unlock", "Unlock camera movement so it resumes following the party.")]
    public class CameraUnlockEvent : GameEvent
    {
    }
}
