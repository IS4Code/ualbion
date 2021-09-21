﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using SerdesNet;
using UAlbion.Api;
using UAlbion.Config;

namespace UAlbion.Formats.Containers
{
    /// <summary>
    /// Container encoding a list of assets to a JSON object/dictionary
    /// </summary>
    public class JsonObjectContainer : IAssetContainer
    {
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The serializer will handle it")]
        public ISerializer Read(string path, AssetInfo info, IFileSystem disk, IJsonUtil jsonUtil)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (disk == null) throw new ArgumentNullException(nameof(disk));
            if (jsonUtil == null) throw new ArgumentNullException(nameof(jsonUtil));
            if (!disk.FileExists(path))
                return null;

            var dict = Load(path, disk, jsonUtil);
            if (!dict.TryGetValue(info.AssetId, out var token))
                return null;

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonUtil.Serialize(token)));
            var br = new BinaryReader(ms);
            return new GenericBinaryReader(
                br,
                ms.Length,
                Encoding.UTF8.GetString,
                ApiUtil.Assert,
                () => { br.Dispose(); ms.Dispose(); });
        }

        public void Write(string path, IList<(AssetInfo, byte[])> assets, IFileSystem disk, IJsonUtil jsonUtil)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (disk == null) throw new ArgumentNullException(nameof(disk));
            if (jsonUtil == null) throw new ArgumentNullException(nameof(jsonUtil));

            var dir = Path.GetDirectoryName(path);
            if (!disk.DirectoryExists(dir))
                disk.CreateDirectory(dir);

            var dict = new Dictionary<string, object>();
            foreach (var (info, bytes) in assets)
            {
                var jObject = jsonUtil.Deserialize<object>(bytes);
                dict[info.AssetId.ToString()] = jObject;
            }

            var fullText = jsonUtil.Serialize(dict);
            disk.WriteAllText(path, fullText);
        }

        public List<(int, int)> GetSubItemRanges(string path, AssetFileInfo info, IFileSystem disk, IJsonUtil jsonUtil)
        {
            if (disk == null) throw new ArgumentNullException(nameof(disk));
            if (jsonUtil == null) throw new ArgumentNullException(nameof(jsonUtil));
            if (!disk.FileExists(path))
                return null;
            var dict = Load(path, disk, jsonUtil);
            return FormatUtil.SortedIntsToRanges(dict.Keys.Select(x => x.Id).OrderBy(x => x));
        }

        static IDictionary<AssetId, object> Load(string path, IFileSystem disk, IJsonUtil jsonUtil)
        {
            var text = disk.ReadAllBytes(path);
            var dict = jsonUtil.Deserialize<IDictionary<string, object>>(text);
            if (dict == null)
                throw new FileLoadException($"Could not deserialize \"{path}\"");

            return dict.ToDictionary(x => AssetId.Parse(x.Key), x => x.Value);
        }
    }
}