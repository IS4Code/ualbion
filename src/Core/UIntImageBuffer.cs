﻿using System;

namespace UAlbion.Core
{
    public readonly ref struct UIntImageBuffer
    {
        public UIntImageBuffer(uint width, uint height, int stride, Span<uint> buffer)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Buffer = buffer;
        }

        public UIntImageBuffer(UIntImageBuffer existing, Span<uint> buffer)
        {
            Width = existing.Width;
            Height = existing.Height;
            Stride = existing.Stride;
            Buffer = buffer;
        }

        public uint Width { get; }
        public uint Height { get; }
        public int Stride { get; }
        public Span<uint> Buffer { get; }
    }
}