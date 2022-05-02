﻿using UAlbion.Api;
using UAlbion.Api.Eventing;

namespace UAlbion.Formats.ScriptEvents;

[Event("fade_from_black", "Fade back into the game world from a black screen.")] // USED IN SCRIPT
public class FadeFromBlackEvent : Event
{
}