﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SerdesNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UAlbion.Api.Eventing;
using UAlbion.Config;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Core.Veldrid;
using UAlbion.Formats;
using UAlbion.Formats.Assets.Labyrinth;
using UAlbion.Formats.Exporters.Tiled;
using UAlbion.Formats.Parsers;
using UAlbion.Game.Events;
using UAlbion.Game.Scenes;
using Veldrid;

namespace UAlbion.Game.Veldrid.Assets;

public sealed class IsometricLabyrinthLoader : Component, IAssetLoader<LabyrinthData>, IDisposable
{
    public const int DefaultWidth = 48;
    public const int DefaultHeight = 64;
    public const int DefaultBaseHeight = 40;
    public const int DefaultTilesPerRow = 16;

    // TODO: Calculate these properly
    const int HackyContentsOffsetX = -143;
    const int HackyContentsOffsetY = 235;

    readonly JsonLoader<LabyrinthData> _jsonLoader = new();
    Engine _engine;
    IsometricBuilder _builder;

    void SetupEngine(int tileWidth, int tileHeight, int baseHeight, int tilesPerRow)
    {
        var (services, builder) = IsometricSetup.SetupEngine(Exchange,
            tileWidth, tileHeight, baseHeight, tilesPerRow,
            GraphicsBackend.Vulkan, false, null);
        AttachChild(services);
        _engine = (Engine)Resolve<IEngine>();
        _builder = builder;
        Raise(new SetSceneEvent(SceneId.IsometricBake));
        Raise(new SetClearColourEvent(0, 0, 0, 0));
        // Raise(new EngineFlagEvent(FlagOperation.Set, EngineFlags.ShowBoundingBoxes));
#pragma warning restore CA2000 // Dispose objects before losing scopes
    }

    IEnumerable<(string, byte[])> Save(LabyrinthData labyrinth, AssetInfo info, LoaderContext context, IsometricMode mode, string pngPath, string tsxPath)
    {
        var tileWidth = info.Get(AssetProperty.TileWidth, DefaultWidth);
        var tileHeight = info.Get(AssetProperty.TileHeight, DefaultHeight);
        var baseHeight = info.Get(AssetProperty.BaseHeight, DefaultBaseHeight);
        var tilesPerRow = info.Get(AssetProperty.TilesPerRow, DefaultTilesPerRow);

        if (_engine == null)
            SetupEngine(tileWidth, tileHeight, baseHeight, tilesPerRow);

        var frames = _builder.Build(labyrinth, info, mode, context.Assets);

        Image<Bgra32> image = _engine.RenderFrame(false, 0);

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        var pngBytes = stream.ToArray();
        yield return (pngPath, pngBytes);

        int expansionFactor = mode == IsometricMode.Contents ? IsometricBuilder.ContentsExpansionFactor : 1;
        var properties = new Tilemap3DProperties
        {
            TilesetId = labyrinth.Id.Id,
            IsoMode = mode,
            TileWidth = expansionFactor * tileWidth,
            TileHeight = expansionFactor * tileHeight,
            ImagePath = pngPath,
            TilesetPath = tsxPath,
            ImageWidth = image.Width,
            ImageHeight = image.Height,
            OffsetX = mode == IsometricMode.Contents ? HackyContentsOffsetX : 0,
            OffsetY = mode == IsometricMode.Contents ? HackyContentsOffsetY : 0
        };

        var tiledTileset = TilesetMapping.FromLabyrinth(labyrinth, properties, frames);
        var tsxBytes = FormatUtil.BytesFromTextWriter(tiledTileset.Serialize);
        yield return (tsxPath, tsxBytes);
    }

    byte[] SaveJson(LabyrinthData labyrinth, AssetInfo info, LoaderContext context) =>
        FormatUtil.SerializeToBytes(s =>
            _jsonLoader.Serdes(labyrinth, info, s, context));

    public LabyrinthData Serdes(LabyrinthData existing, AssetInfo info, ISerializer s, LoaderContext context)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var json = info.GetPattern(AssetProperty.Pattern, "{0}_{2}.json");
        var path = new AssetPath(info);
        string BuildPath(AssetPathPattern pattern) => pattern.Format(path);

        if (s.IsReading())
        {
            var chunks = PackedChunks.Unpack(s);
            var (chunk, _) = chunks.Single();

            return FormatUtil.DeserializeFromBytes(chunk, s2 =>
                _jsonLoader.Serdes(null, info, s2, context));
        }

        if (existing == null) throw new ArgumentNullException(nameof(existing));
        var floorTsx    = info.GetPattern(AssetProperty.TiledFloorPattern,    "Tiled/{0}_{2}_Floors.tsx");
        var ceilingTsx  = info.GetPattern(AssetProperty.TiledCeilingPattern,  "Tiled/{0}_{2}_Ceilings.tsx");
        var wallTsx     = info.GetPattern(AssetProperty.TiledWallPattern,     "Tiled/{0}_{2}_Walls.tsx");
        var contentsTsx = info.GetPattern(AssetProperty.TiledContentsPattern, "Tiled/{0}_{2}_Contents.tsx");
        var floorPng    = info.GetPattern(AssetProperty.FloorPngPattern,      "Tiled/Gfx/{0}_{2}_Floors.png");
        var ceilingPng  = info.GetPattern(AssetProperty.CeilingPngPattern,    "Tiled/Gfx/{0}_{2}_Ceilings.png");
        var wallPng     = info.GetPattern(AssetProperty.WallPngPattern,       "Tiled/Gfx/{0}_{2}_Walls.png");
        var contentsPng = info.GetPattern(AssetProperty.ContentsPngPattern,   "Tiled/Gfx/{0}_{2}_Contents.png");

        var files = new List<(string, byte[])> {(json.Format(path), SaveJson(existing, info, context))};
        files.AddRange(Save(existing, info, context, IsometricMode.Floors,   BuildPath(floorPng),    BuildPath(floorTsx)));
        files.AddRange(Save(existing, info, context, IsometricMode.Ceilings, BuildPath(ceilingPng),  BuildPath(ceilingTsx)));
        files.AddRange(Save(existing, info, context, IsometricMode.Walls,    BuildPath(wallPng),     BuildPath(wallTsx)));
        files.AddRange(Save(existing, info, context, IsometricMode.Contents, BuildPath(contentsPng), BuildPath(contentsTsx)));

        PackedChunks.PackNamed(s, files.Count, i => (files[i].Item2, files[i].Item1));
        return existing;
    }

    public object Serdes(object existing, AssetInfo info, ISerializer s, LoaderContext context)
        => Serdes((LabyrinthData)existing, info, s, context);

    public void Dispose() => _engine?.Dispose();
}
