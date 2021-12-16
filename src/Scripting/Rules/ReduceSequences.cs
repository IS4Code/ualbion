﻿using System;

namespace UAlbion.Scripting.Rules
{
    public static class ReduceSequences
    {
        const string Description = "Reduce sequence";
        public static (ControlFlowGraph, string) Decompile(ControlFlowGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            foreach (var index in graph.GetDfsPostOrder())
            {
                var children = graph.Children(index);
                if (children.Length != 1 || children[0] == index)
                    continue;

                int child = children[0];
                // Func<string> vis = () => graph.ToVis().AddPointer("index", index).AddPointer("child", child).ToString(); // For VS Code debug visualisation

                var childsParents = graph.Parents(child);
                var grandChildren = graph.Children(child);

                if (childsParents.Length != 1 || grandChildren.Length > 1)
                    continue; // Is a jump target from somewhere else as well - can't combine

                if (grandChildren.Length == 1 && (grandChildren[0] == index || grandChildren[0] == child))
                    continue; // Loops around, not a sequence

                var node = graph.Nodes[index];
                var childNode = graph.Nodes[child];

                var updated = graph
                    .RemoveNode(child)
                    .ReplaceNode(index, Emit.Seq(node, childNode));

                foreach (var grandChild in grandChildren)
                    updated = updated.AddEdge(index, grandChild, graph.GetEdgeLabel(child, grandChild));

                return (updated, Description);
            }

            return (graph, null);
        }
    }
}