﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAlbion.Config;
using UAlbion.Core;
using UAlbion.Game.Assets;

namespace UAlbion
{
    static class ConvertAssets
    {
        static (ModApplier, EventExchange) BuildModApplier(string baseDir, string mod)
        {
            var config = GeneralConfig.Load(Path.Combine(baseDir, "data", "config.json"), baseDir);
            var applier = new ModApplier();
            var exchange = new EventExchange(new LogExchange());
            exchange
                .Register<IGeneralConfig>(config)
                .Attach(new StdioConsoleLogger())
                .Attach(new AssetLoaderRegistry())
                .Attach(new ContainerRegistry())
                .Attach(new AssetLocator())
                .Attach(applier)
                ;
            applier.LoadMods(config, new[] { mod });
            return (applier, exchange);
        }

        public static void Convert(
            string baseDir,
            string fromMod,
            string toMod,
            string[] ids,
            ISet<AssetType> assetTypes)
        {
            var (from, fromExchange) = BuildModApplier(baseDir, fromMod);
            var (to, toExchange) = BuildModApplier(baseDir, toMod);

            using (fromExchange)
            using (toExchange)
            {
                toExchange.Attach(new AssetManager(from));
                var parsedIds = ids?.Select(AssetId.Parse).ToHashSet();
                var paletteHints = PaletteHints.Load(Path.Combine(baseDir, "mods", "Base", "palette_hints.json"));

                (object, AssetInfo) LoaderFunc(AssetId x, string lang) => (from.LoadAsset(x, lang), from.GetAssetInfo(x, lang));

                to.SaveAssets(LoaderFunc, paletteHints, parsedIds, assetTypes);
            }
        }
    }
}
