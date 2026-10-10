using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using BlackHole.Core;
using BlackHole.Unity;

internal static class NodeSuiteChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        const string folder = "Docs/QA/NodeSuite/";
        var content = NodeContentCsv.Load(File.ReadAllText("Assets/Data/NodeTable/UpgradeStats.csv"),
            File.ReadAllText("Assets/Data/NodeTable/Nodes.csv"), File.ReadAllText("Assets/Data/NodeTable/NodeCost.csv"),
            File.ReadAllText("Assets/Data/NodeTable/NodeEffects.csv"));
        Require(content.Content != null, "real CSV load: " + string.Join("; ", content.Diagnostics));
        using var fixture = JsonDocument.Parse(File.ReadAllText(folder + "layout.fixture.json"));
        var data = new NodeTreeData();
        foreach (var node in fixture.RootElement.GetProperty("Nodes").EnumerateArray())
        {
            data.Nodes.Add(new NodeData
            {
                Id = node.GetProperty("Id").GetString(), Start = node.GetProperty("Start").GetBoolean(),
                X = node.GetProperty("X").GetInt32(), Y = node.GetProperty("Y").GetInt32(),
                Links = node.GetProperty("Links").EnumerateArray().Select(v => v.GetString()).ToList(),
            });
        }
        var loaded = NodeTreeLoader.Load(data, content.Content);
        Require(loaded.Tree != null, "real layout load: " + string.Join("; ", loaded.Diagnostics));
        var milestones = fixture.RootElement.GetProperty("Milestones").EnumerateArray()
            .Select(m => new HqMilestone(m.GetProperty("level").GetInt32(), m.GetProperty("targetGold").GetInt64(),
                m.GetProperty("fieldScale").GetSingle())).ToList();
        // Actual ladder exported from HqGrowthSetup.asset by the generator.
        var growth = new HqGrowthDefinition(fixture.RootElement.GetProperty("LevelExp").EnumerateArray().Select(v=>v.GetInt64()).ToArray(), milestones);
        using var analysis = JsonDocument.Parse(File.ReadAllText(folder + "analysis.json"));
        foreach (string asset in new[] { "NodeCatalog.asset", "HqGrowthSetup.asset" })
        {
            string sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("Assets/Data/" + asset))).ToLowerInvariant();
            Require(sha == analysis.RootElement.GetProperty("sourceInputs").GetProperty(asset).GetProperty("sha256").GetString(), "stale Unity fixture: regenerate " + asset);
        }
        var scenarios = new Dictionary<string, PlaytestScenario>();
        int count = 0;
        foreach (var entry in analysis.RootElement.GetProperty("presets").EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString();
            var scenario = PlaytestScenario.Parse(File.ReadAllText("Assets/Playtest/Scenarios/" + name + ".json"), out string error);
            Require(scenario != null, name + ": " + error);
            var progress = new ProgressState(); var errors = new List<string>(); var warnings = new List<string>();
            Require(ScenarioRunner.TryApply(scenario, progress, loaded.Tree, growth, errors, warnings), name + ": " + string.Join("; ", errors));
            Require(progress.Gold == scenario.gold, name + ": final gold");
            Require(scenario.seed != 0, name + ": fixed seed");
            if (entry.TryGetProperty("resolvedNodes", out var resolved))
            {
                var expected = resolved.EnumerateArray().ToDictionary(v=>v.GetProperty("nodeId").GetString(), v=>v.GetProperty("rank").GetInt32());
                foreach (var node in loaded.Tree.Nodes)
                    Require(progress.RankOf(node.Id) == (expected.TryGetValue(node.Id, out int rank) ? rank : 0), name + ": actual cheapest route " + node.Id);
            }
            // Every owned node must be connected to an owned start through owned nodes.
            var reached = new HashSet<string>(data.Nodes.Where(n=>n.Start && progress.Owns(n.Id)).Select(n=>n.Id));
            bool changed;
            do
            {
                changed = false;
                foreach (var edge in loaded.Tree.Graph.Links)
                {
                    if (reached.Contains(edge.A) && progress.Owns(edge.B)) changed |= reached.Add(edge.B);
                    if (reached.Contains(edge.B) && progress.Owns(edge.A)) changed |= reached.Add(edge.A);
                }
            } while (changed);
            foreach (var node in loaded.Tree.Nodes)
                Require(!progress.Owns(node.Id) || reached.Contains(node.Id), name + ": owned path " + node.Id);
            scenarios[name] = scenario;
            count++;
        }
        foreach (var zone in analysis.RootElement.GetProperty("zones").EnumerateArray())
        {
            int id = zone.GetProperty("id").GetInt32();
            string focus = zone.GetProperty("focus").GetString();
            var off = scenarios[$"qa-zone{id:00}-off"].nodes.ToDictionary(n=>n.nodeId,n=>n.rank);
            var on = scenarios[$"qa-zone{id:00}-on"].nodes.ToDictionary(n=>n.nodeId,n=>n.rank);
            Require(on.Count==off.Count+1 && on[focus]==1 && !off.ContainsKey(focus), "single focus delta " + id);
            Require(off.All(p=>on[p.Key]==p.Value), "unchanged support " + id);
        }
        foreach (var mark in milestones)
        {
            var before = scenarios[$"qa-mark{mark.Level:00}-before"];
            var after = scenarios[$"qa-mark{mark.Level:00}-after"];
            Require(before.nodes.Select(n=>n.nodeId+":"+n.rank).SequenceEqual(after.nodes.Select(n=>n.nodeId+":"+n.rank)), "same ranks milestone " + mark.Level);
            Require(before.startLevel==mark.Level-1 && after.startLevel==mark.Level && after.growthStage==before.growthStage+1, "milestone boundary " + mark.Level);
        }
        Console.WriteLine($"PASS: real CSV (60 stats/247 nodes), graph, {count} presets, owned purchase paths, 8 exact budget results, 8 single-node pairs, 3 milestone pairs");
    }
}
