﻿using UAlbion.Api;

namespace UAlbion.Core.Events
{
    [Event("e:set_msaa", "Sets the multisample anti-aliasing level")]
    public class SetMsaaLevelEvent : IEvent
    {
        [EventPart("sample_count", "Valid values are 1, 2, 4, 8, 16 and 32")]
        public int SampleCount { get; }

        public SetMsaaLevelEvent(int msaaOption)
        {
            SampleCount = msaaOption;
        }
    }
}
