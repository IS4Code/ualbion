﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UAlbion.Api;
using UAlbion.Config;
using UAlbion.Core;
using UAlbion.Formats;
using UAlbion.Formats.Assets;
using UAlbion.Formats.Config;
using UAlbion.Formats.MapEvents;
using UAlbion.Game;
using UAlbion.Game.Settings;
using UAlbion.Scripting;
using UAlbion.Scripting.Ast;
using UAlbion.Scripting.Tests;
using UAlbion.TestCommon;
using Xunit;

namespace UAlbion.Base.Tests
{
    public class FullDecompilationTests : IDisposable
    {
        static readonly string ResultsDir = Path.Combine(TestUtil.FindBasePath(), "re", "FullDecomp");
        static int s_testNum;

        static readonly IJsonUtil JsonUtil = new FormatJsonUtil();
        static readonly CoreConfig CoreConfig;
        static readonly GeneralConfig GeneralConfig;
        static readonly GameConfig GameConfig;
        static readonly GeneralSettings Settings;
        readonly int _testNum;

        static FullDecompilationTests()
        {
            var disk = new MockFileSystem(true);
            var baseDir = ConfigUtil.FindBasePath(disk);
            GeneralConfig = AssetSystem.LoadGeneralConfig(baseDir, disk, JsonUtil);
            CoreConfig = new CoreConfig();
            GameConfig = AssetSystem.LoadGameConfig(baseDir, disk, JsonUtil);
            Settings = new GeneralSettings
            {
                ActiveMods = { "Base" },
                Language = Language.English
            };
        }

        public FullDecompilationTests()
        {
            Event.AddEventsFromAssembly(typeof(ActionEvent).Assembly);
            AssetMapping.GlobalIsThreadLocal = true;
            AssetMapping.Global.Clear();
            _testNum = Interlocked.Increment(ref s_testNum);
            PerfTracker.StartupEvent($"Start decompilation test {_testNum}");
        }
        public void Dispose()
        {
            PerfTracker.StartupEvent($"Finish decompilation test {_testNum}");
        }

        [Fact] public void Set1() => TestEventSet(new EventSetId(AssetType.EventSet, 1));
        [Fact] public void Map110() => TestMap(new MapId(AssetType.Map, 110));

        static void TestMap(MapId id)
        {
            var map = Load(x => x.LoadMap(id));
            var npcRefs = map.Npcs.Where(x => x.Node != null).Select(x => x.Node.Id).ToHashSet();
            var zoneRefs = map.Zones.Where(x => x.Node != null).Select(x => x.Node.Id).ToHashSet();
            var refs = npcRefs.Union(zoneRefs).Except(map.Chains);

            TestInner(
                map.Events,
                map.Chains,
                refs);
        }

        static void TestEventSet(EventSetId id)
        {
            var set = Load(x => x.LoadEventSet(id));
            TestInner(set.Events, set.Chains, Array.Empty<ushort>());
        }

        static void TestInner<T>(
            IList<T> events,
            IEnumerable<ushort> chains,
            IEnumerable<ushort> entryPoints,
            [CallerMemberName] string testName = null) where T : IEventNode
        {
            var resultsDir = Path.Combine(ResultsDir, testName ?? "Unknown");
            var graphs = Decompiler.BuildEventRegions(events, chains, entryPoints);
            var scripts = new string[graphs.Count];
            var errors = new string[graphs.Count];
            var allSteps = new List<List<(string, ControlFlowGraph)>>();
            int successCount = 0;

            for (var index = 0; index < graphs.Count; index++)
            {
                errors[index] = "";
                var steps = new List<(string, ControlFlowGraph)>();
                allSteps.Add(steps);
                var graph = graphs[index];
                try
                {
                    var decompiled = Decompile(graph, steps);
                    var visitor = new FormatScriptVisitor();
                    decompiled.Accept(visitor);
                    scripts[index] = visitor.Code;

                    var roundTripLayout = ScriptCompiler.Compile(scripts[index], steps);
                    var expectedLayout = EventLayout.Build(new[] { graph });

                    if (!TestUtil.CompareLayout(roundTripLayout, expectedLayout, out var error))
                        errors[index] += $"[{index}: {error}] ";
                    else
                        successCount++;
                }
                catch (ControlFlowGraphException ex)
                {
                    steps.Add((ex.Message, ex.Graph));
                    errors[index] += $"[{index}: {ex.Message}] ";
                }
                catch (Exception ex)
                {
                    errors[index] += $"[{index}: {ex.Message}] ";
                }
            }

            if (successCount < graphs.Count)
            {
                var combined = string.Join(Environment.NewLine, errors.Where(x => x.Length > 0));
                for (int i = 0; i < allSteps.Count; i++)
                {
                    var steps = allSteps[i];
                    if (!string.IsNullOrEmpty(errors[i]))
                        TestUtil.DumpSteps(steps, resultsDir, $"Region{i}");
                }

                throw new InvalidOperationException($"[{successCount}/{graphs.Count}] Errors:{Environment.NewLine}{combined}");
            }
        }

        static ICfgNode Decompile(ControlFlowGraph graph, List<(string, ControlFlowGraph)> steps)
        {
            ControlFlowGraph Record(string description, ControlFlowGraph g)
            {
                if (steps.Count == 0 || steps[^1].Item2 != g)
                    steps.Add((description, g));
                return g;
            }

            return Decompiler.SimplifyGraph(graph, Record);
        }

        static T Load<T>(Func<IAssetManager, T> func)
        {
            var disk = new MockFileSystem(true);
            var exchange = AssetSystem.Setup(disk, JsonUtil, GeneralConfig, Settings, CoreConfig, GameConfig);

            var assets = exchange.Resolve<IAssetManager>();
            var result = func(assets);
            Assert.NotNull(result);

            return result;
        }
    }
}
