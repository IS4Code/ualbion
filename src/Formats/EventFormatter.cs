﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UAlbion.Api;
using UAlbion.Config;
using UAlbion.Formats.Assets;
using UAlbion.Formats.MapEvents;
using UAlbion.Scripting;

namespace UAlbion.Formats
{
    public class EventFormatter
    {
        readonly Func<StringId, string> _stringLoadFunc;
        readonly AssetId _textSourceId;

        public EventFormatter(Func<StringId, string> stringLoadFunc, AssetId textSourceId)
        {
            _stringLoadFunc = stringLoadFunc;
            _textSourceId = textSourceId;
        }

        public string Format(IEventNode e, int idOffset = 0)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var nodeText = e.ToString(idOffset);
            if (e.Event is MapTextEvent textEvent)
            {
                var text = _stringLoadFunc(new StringId(_textSourceId, textEvent.SubId));
                return $"{nodeText} // \"{text}\"";
            }

            return nodeText;
        }

        public void FormatEventSetDecompiled<T>(
            StringBuilder sb,
            IList<T> events,
            IEnumerable<ushort> chains,
            IEnumerable<ushort> additionalEntryPoints,
            int indent) where T : IEventNode
        {
            List<(string, IGraph)> steps = new();
            try
            {
                var trees = Decompiler.Decompile(events, chains, additionalEntryPoints, steps);
                var counter = new CountEventsVisitor();

                bool first = true;
                bool lastWasTrivial = false;
                foreach (var tree in trees)
                {
                    if (!first)
                        sb.AppendLine();

                    tree.Accept(counter);
                    var eventCount = counter.Count;
                    counter.Reset();

                    if (eventCount > 1)
                    {
                        if (lastWasTrivial)
                            sb.AppendLine();
                        sb.AppendLine("{");

                        var visitor = new FormatScriptVisitor(sb) { PrettyPrint = true, IndentLevel = indent };
                        visitor.IndentLevel += visitor.TabSize;
                        tree.Accept(visitor);

                        sb.AppendLine();
                        sb.AppendLine("}");
                        lastWasTrivial = false;
                    }
                    else
                    {
                        sb.Append("{ ");
                        var visitor = new FormatScriptVisitor(sb) { PrettyPrint = false };
                        tree.Accept(visitor);
                        sb.Append(" }");
                        lastWasTrivial = true;
                    }
                    first = false;
                }
            }
            catch (ControlFlowGraphException)
            {
                FormatEventSet(sb, events, indent); // Fallback to raw view
            }
        }

        static HashSet<IEventNode> ExploreGraph(IEventNode head)
        {
            var uniqueEvents = new HashSet<IEventNode>();
            void Visit(IEventNode e)
            {
                while (true)
                {
                    if (e == null)
                        return;

                    if (!uniqueEvents.Add(e))
                        break;

                    if (e is IBranchNode branch)
                        Visit(branch.NextIfFalse);
                    e = e.Next;
                }
            }

            Visit(head);
            return uniqueEvents;
        }

        public string FormatChain(IEventNode firstEvent, int indent = 0)
        {
            var sb = new StringBuilder();
            FormatChain(sb, firstEvent, indent);
            return sb.ToString();
        }

        public void FormatChain(StringBuilder sb, IEventNode firstEvent, int indent = 0)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (firstEvent == null) return;

            var uniqueEvents = ExploreGraph(firstEvent);
            var sorted = uniqueEvents.OrderBy(x => x.Id).ToList();
            foreach (var e in sorted)
            {
                sb.Append(new string(' ', 4 * indent));
                sb.AppendLine(Format(e, sorted[0].Id));
            }
        }

        public void FormatEventSet<T>(StringBuilder sb, IList<T> events, int indent = 0) where T : IEventNode
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (events == null) return;

            foreach (var e in events)
            {
                sb.Append(new string(' ', 4 * indent));
                sb.AppendLine(Format(e, events[0].Id));
            }
        }
    }
}
