﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UAlbion.Formats.Parsers
{
    public class Map2D
    {
        [Flags]
        public enum TriggerType : ushort
        {
            Normal    = 1 << 0,
            Examine   = 1 << 1,
            Touch     = 1 << 2,
            Speak     = 1 << 3,
            UseItem   = 1 << 4,
            MapInit   = 1 << 5,
            EveryStep = 1 << 6,
            EveryHour = 1 << 7,
            EveryDay  = 1 << 8,
            Default   = 1 << 9,
            Action    = 1 << 10,
            Npc       = 1 << 11,
            Take      = 1 << 12,
            Unk13     = 1 << 13,
            Unk14     = 1 << 14,
            Unk15     = 1 << 15,
        }

        [Flags]
        public enum MovementType : byte
        {
            Random1 = 1,
            Random2 = 2,
            RandomMask = 3,
            FollowParty = 4
        }

        public class Npc
        {
            public byte Id { get; set; }
            public byte Sound { get; set; }
            public ushort? EventNumber { get; set; }
            public ushort ObjectNumber { get; set; }
            public int Flags { get; set; } // 1=Dialogue, 2=AutoAttack, 11=ReturnMsg
            public MovementType MovementType { get; set; }
            public ushort Unk8 { get; set; }
            public int Unk9 { get; set; }
            public Waypoint[] Waypoints { get; set; }
        }

        public enum EventType : byte
        {
            Script            = 0,
            MapExit           = 1,
            Door              = 2,
            Chest             = 3,
            Text              = 4,
            Spinner           = 5,
            Trap              = 6,
            ChangeUsedItem    = 7,
            DataChange        = 8,
            ChangeIcon        = 9,
            Encounter         = 0xA,
            PlaceAction       = 0xB,
            Query             = 0xC,
            Modify            = 0xD,
            Action            = 0xE,
            Signal            = 0xF,
            CloneAutomap      = 0x10,
            Sound             = 0x11,
            StartDialogue     = 0x12,
            CreateTransport   = 0x13,
            Execute           = 0x14,
            RemovePartyMember = 0x15,
            EndDialogue       = 0x16,
            Wipe              = 0x17,
            PlayAnimation     = 0x18,
            Offset            = 0x19,
            Pause             = 0x1A,
            SimpleChest       = 0x1B,
            AskSurrender      = 0x1C,
            DoScript          = 0x1D
        }

        public class Event
        {
            //public int Id;
            public EventType EventType { get; set; }
            public byte Unk1;
            public byte Unk2;
            public byte Unk3;
            public byte Unk4;
            public byte Unk5;
            public ushort Unk6;
            public ushort Unk8;
            public ushort? NextEventId;
        }

        public class Zone
        {
            public bool Global;
            public ushort X;
            public ushort Y;
            public TriggerType Trigger;
            public ushort EventNumber;
        }

        public struct Waypoint
        {
            public byte X;
            public byte Y;
        }


        public IList<Npc> Npcs { get; } = new List<Npc>();
        public int[] Underlay { get; set; }
        public int[] Overlay { get; set; }
        public IList<Event> Events { get; } = new List<Event>();
        public IList<Zone> Zones { get; } = new List<Zone>();

        public byte Unk0 { get; set; } // Wait/Rest, Light-Environment, NPC converge range
        public byte Sound { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TilesetId { get; set; }
        public int CombatBackgroundId { get; set; }
        public int PaletteId { get; set; }
        public byte FrameRate { get; set; }
    }

    [AssetLoader(XldObjectType.Map2D)]
    public class Map2DLoader : IAssetLoader
    {
        public object Load(BinaryReader br, long streamLength, string name, AssetConfig.Asset config)
        {
            var startPosition = br.BaseStream.Position;
            var map = new Map2D();
            map.Unk0 = br.ReadByte(); // 0

            int npcCount = br.ReadByte(); // 1
            if (npcCount == 0) npcCount = 0x20;
            else if (npcCount == 0x40) npcCount = 0x60;

            var mapType = br.ReadByte(); // 2
            Debug.Assert(2 == mapType); // 1 = 3D

            map.Sound     = br.ReadByte(); //3
            map.Width     = br.ReadByte(); //4
            map.Height    = br.ReadByte(); //5
            map.TilesetId = br.ReadByte() - 1; //6
            map.CombatBackgroundId = br.ReadByte(); //7
            map.PaletteId = br.ReadByte() - 1; //8
            map.FrameRate = br.ReadByte(); //9

            for (int i = 0; i < npcCount; i++)
            {
                var npc = new Map2D.Npc();
                npc.Id = br.ReadByte(); // +0
                npc.Sound = br.ReadByte(); // +1
                npc.EventNumber = br.ReadUInt16(); // +2
                if (npc.EventNumber == 0xffff) npc.EventNumber = null;

                npc.ObjectNumber = br.ReadUInt16(); // +4
                npc.Flags = br.ReadByte(); // +6 // Combine this & MovementType ?
                npc.MovementType = (Map2D.MovementType)br.ReadByte(); // +7
                npc.Unk8 = br.ReadByte(); // +8
                npc.Unk9 = br.ReadByte(); // +9
                map.Npcs.Add(npc);
            }

            map.Underlay = new int[map.Width * map.Height];
            map.Overlay = new int[map.Width * map.Height];
            for (int i = 0; i < map.Width * map.Height; i++)
            {
                byte b1 = br.ReadByte();
                byte b2 = br.ReadByte();
                byte b3 = br.ReadByte();

                map.Overlay[i] = (b1 << 4) + (b2 >> 4);
                map.Underlay[i] = ((b2 & 0x0F) << 8) + b3;
            }

            int zoneCount = br.ReadUInt16();
            for (int i = 0; i < zoneCount; i++)
            {
                var zone = new Map2D.Zone();
                zone.Global = true;
                zone.X = br.ReadUInt16(); // +0
                Debug.Assert(zone.X == 0);
                zone.Trigger = (Map2D.TriggerType)br.ReadUInt16(); // +2
                zone.EventNumber = br.ReadUInt16(); // +4
                map.Zones.Add(zone);
            }

            for (int j = 0; j < map.Height; j++)
            {
                zoneCount = br.ReadUInt16();
                for (int i = 0; i < zoneCount; i++)
                {
                    var zone = new Map2D.Zone();
                    zone.X = br.ReadByte(); // +0
                    var unk1 = br.ReadByte(); // +1
                    zone.Y = (ushort)j;
                    zone.Trigger = (Map2D.TriggerType)br.ReadUInt16(); // +2
                    zone.EventNumber = br.ReadUInt16(); // +4
                    map.Zones.Add(zone);
                }
            }

            int eventCount = br.ReadUInt16();
            for (int i = 0; i < eventCount; i++)
            {
                var e = new Map2D.Event();
                e.EventType = (Map2D.EventType)br.ReadByte(); // +0
                e.Unk1 = br.ReadByte(); // +1
                e.Unk2 = br.ReadByte(); // +2
                e.Unk3 = br.ReadByte(); // +3
                e.Unk4 = br.ReadByte(); // +4
                e.Unk5 = br.ReadByte(); // +5
                e.Unk6 = br.ReadUInt16(); // +6
                e.Unk8 = br.ReadUInt16(); // +8
                e.NextEventId = br.ReadUInt16(); // +A
                if (e.NextEventId == 0xffff) e.NextEventId = null;
                map.Events.Add(e);
            }

            foreach(var npc in map.Npcs)
            {
                if ((npc.MovementType & Map2D.MovementType.RandomMask) != 0)
                {
                    var wp = new Map2D.Waypoint();
                    wp.X = br.ReadByte();
                    wp.Y = br.ReadByte();
                    npc.Waypoints = new[] { wp };
                }
                else
                {
                    npc.Waypoints = new Map2D.Waypoint[0x480];
                    for(int i =0; i < npc.Waypoints.Length; i++)
                    {
                        npc.Waypoints[i].X = br.ReadByte();
                        npc.Waypoints[i].Y = br.ReadByte();
                    }
                }

            }

            return map;
        }
    }
}