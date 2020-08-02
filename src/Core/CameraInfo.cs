﻿using System.Numerics;
using System.Runtime.InteropServices;

namespace UAlbion.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CameraInfo
    {
        public Vector3 WorldSpacePosition;
        readonly uint _padding1;

        public float CameraYaw;
        public float CameraPitch;
        public float CameraRoll;
        readonly uint _padding2;

        public Vector2 Resolution;

        public float Time;
        public float Special1;
        public float Special2;
        public uint EngineFlags;
        readonly uint _padding3;
        readonly uint _padding4;
    }
}
