﻿using System;

namespace UAlbion.Formats.Assets
{
    [Flags]
    public enum ItemFlags : ushort
    {
        Unk0 = 1,
        PlotItem = 1 << 1,
        Unk2 = 1 << 2,
        Unk3 = 1 << 3,
        Unk4 = 1 << 4,
        Unk5 = 1 << 5,
        Unk6 = 1 << 6,
        Unk7 = 1 << 7,
        Unk8 = 1 << 8,
        Unk9 = 1 << 9,
        Cursed = 1 << 10,
        Unk11 = 1 << 11,
        Unk12 = 1 << 12,
    }
}
