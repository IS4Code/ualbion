﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UAlbion.Formats.AssetIds;

namespace UAlbion.Game.Text
{
    public class DynamicText : IText
    {
        public delegate IEnumerable<TextBlock> GeneratorFunc();
        readonly GeneratorFunc _generator;
        readonly Func<int, int> _getVersion;
#if DEBUG
        string _lastText;
        public override string ToString()
        {
            if (_lastText != null)
                return _lastText;

            var sb = new StringBuilder();
            int blockId = 0;
            var words = new List<WordId>();

            void WriteWords()
            {
                if (words.Any())
                {
                    sb.Append(" (");
                    sb.Append(string.Join(", ", words.Select(x => x.ToString())));
                    sb.Append(")");
                    words.Clear();
                }
            }

            foreach (var block in _generator())
            {
                if (block.BlockId != blockId)
                {
                    WriteWords();
                    sb.AppendLine();
                    sb.Append("Block");
                    sb.Append(block.BlockId);
                    sb.Append(": ");
                    blockId = block.BlockId;
                }

                foreach (var word in block.Words)
                    words.Add(word);
                sb.Append(block.Text);
            }

            WriteWords();
            _lastText = sb.ToString();
            return _lastText;
        }
#endif
        int _version = 1;

        public DynamicText(GeneratorFunc generator)
        {
            _generator = generator;
            _getVersion = x => _version;
        }
        public DynamicText(GeneratorFunc generator, Func<int, int> getVersion)
        {
            _generator = generator;
            _getVersion = getVersion;
        }

        public int Version => _getVersion(_version);
        public void Invalidate() => _version++;
        public IEnumerable<TextBlock> Get() => _generator();
    }
}
