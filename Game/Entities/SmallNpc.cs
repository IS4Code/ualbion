﻿using System.Numerics;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Core.Visual;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets;

namespace UAlbion.Game.Entities
{
    public class SmallNpc : Component
    {
        static readonly HandlerSet Handlers = new HandlerSet(
            H<SmallNpc, SlowClockEvent>((x, e) => { x._sprite.Frame = e.FrameCount; }));

        readonly MapNpc.Waypoint[] _waypoints;
        readonly MapSprite<SmallNpcId> _sprite;
        public override string ToString() => $"SNpc {_sprite.Id}";

        public SmallNpc(SmallNpcId id, MapNpc.Waypoint[] waypoints) : base(Handlers)
        {
            _waypoints = waypoints;
            _sprite = new MapSprite<SmallNpcId>(id, DrawLayer.Characters1, 0, SpriteFlags.BottomAligned);
            Children.Add(_sprite);
        }

        public override void Subscribed()
        {
            _sprite.TilePosition = new Vector3(_waypoints[0].X, _waypoints[0].Y, DrawLayer.Characters1.ToZCoordinate(_waypoints[0].Y));
            base.Subscribed();
        }
    }
}
