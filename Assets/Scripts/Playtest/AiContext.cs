#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // AI 묶음을 만드는 데 필요한 것. 에디터(AiContextService)가 채우고, 헤드리스 테스트도 같은 것으로 만든다.
    internal sealed class AiContextInput
    {
        public PlaytestScenario Setup;
        public string SetupKey;
        // 이 세팅 키의 메모 원문(최근 것이 앞).
        public List<JsonObject> Notes = new List<JsonObject>();
        // 변경 기록 원문(최근 것이 앞).
        public List<JsonObject> Changes = new List<JsonObject>();

        // 원본(프로필 없음): 저작 데이터(값 목록·이전 값)와 불러온 정의(확정 정보·흐름).
        public ContentData BaseData;
        public NodeContentData BaseNodes;
        public GameContent BaseContent;
        public NodeTree BaseTree;
        public string BaseFingerprint;

        // 창·패널에서 고른 프로필(원본이면 "").
        public string SelectedProfile = string.Empty;

        // AI 초안(없으면 Draft가 null). 패치·검사에 실패했으면 DraftErrors가 있고 DraftContent는 null이다.
        public BalanceProfile Draft;
        public List<string> DraftErrors = new List<string>();
        public GameContent DraftContent;
        public NodeTree DraftTree;
        public string DraftFingerprint;

        public DateTime NowUtc = DateTime.UtcNow;
    }

    // AI가 읽는 묶음(PlaytestData/context/<setupKey>.json, schema 1, M4). 이 파일 하나로 판단할 수 있게 숫자를 모두 미리 담는다.
    // 읽는 법은 Docs/BalanceLoop/AI-GUIDE.md.
    internal static class AiContext
    {
        public const int Schema = 1;
        public const string Kind = "blackhole-ai-context";
        public const int MaxNotes = 20;
        public const int MaxChanges = 10;

        public static JsonObject Build(AiContextInput input)
        {
            TestSetupReport report = TestSetupPreview.Build(input.Setup, input.BaseContent, input.BaseTree);

            var context = new JsonObject
            {
                { "schema", Schema },
                { "kind", Kind },
                { "generatedAtUtc", input.NowUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
                { "guide", "Docs/BalanceLoop/AI-GUIDE.md" },
                { "setupKey", input.SetupKey },
                { "setup", SetupJson(input.Setup) },
                {
                    "content", new JsonObject
                    {
                        { "baseFingerprint", input.BaseFingerprint },
                        { "selectedProfile", input.SelectedProfile ?? string.Empty },
                        { "draft", input.Draft != null ? BalanceProfile.DraftName : null },
                    }
                },
                { "notes", Take(input.Notes, MaxNotes) },
                { "report", ReportJson(report) },
                { "simulation", PlaytestSimulation.Run(input.Setup, input.BaseContent, input.BaseTree).ToJson() },
                { "nodes", NodesJson(report, input.BaseTree, input.BaseNodes) },
                { "values", ValuesJson(input.BaseData, input.BaseNodes) },
                { "draft", input.Draft != null ? DraftJson(input, report) : null },
                { "changes", Take(input.Changes, MaxChanges) },
            };

            return context;
        }

        // 메모의 setup 칸 → 세팅. 읽지 못하면 null.
        public static PlaytestScenario SetupFromNote(JsonObject note)
        {
            JsonObject setup = note?.Object("setup");

            if (setup == null)
                return null;

            var scenario = new PlaytestScenario
            {
                name = setup.Text("name") ?? "note",
                growthStage = setup.Int("growthStage") ?? 0,
                startLevel = setup.Int("startLevel") ?? 0,
                seed = setup.Int("seed") ?? 0,
                gold = (long)(setup.Number("gold") ?? 0),
            };

            foreach (object item in setup.Array("nodes") ?? new List<object>())
            {
                if (item is JsonObject node && node.Text("nodeId") is string id)
                    scenario.nodes.Add(new PlaytestScenario.Node { nodeId = id, rank = node.Int("rank") ?? 0 });
            }

            return scenario;
        }

        private static JsonObject SetupJson(PlaytestScenario setup)
        {
            var nodes = new List<object>();
            foreach (PlaytestScenario.Node node in setup.nodes)
                nodes.Add(new JsonObject { { "nodeId", node.nodeId }, { "rank", node.rank } });

            return new JsonObject
            {
                { "name", setup.name },
                { "growthStage", setup.growthStage },
                { "startLevel", setup.startLevel },
                { "seed", setup.seed },
                { "gold", setup.gold },
                { "nodes", nodes },
            };
        }

        private static JsonObject ReportJson(TestSetupReport report)
        {
            var sections = new List<object>();

            foreach (TestSetupReport.Section section in report.Sections)
            {
                var rows = new JsonObject();
                foreach ((string label, string value) in section.Rows)
                    rows.Add(label, value);

                sections.Add(new JsonObject { { "title", section.Title }, { "rows", rows } });
            }

            return new JsonObject
            {
                { "errors", new List<object>(report.Errors) },
                { "warnings", new List<object>(report.Warnings) },
                { "ownedNodes", report.OwnedNodes },
                { "ownedRanks", report.OwnedRanks },
                { "totalCost", report.TotalCost },
                { "unreachable", new List<object>(report.Unreachable) },
                { "sections", sections },
            };
        }

        // 산 노드(실제로 적용된 Rank)마다: 산 Rank들과 다음 Rank 하나의 비용·효과(경로 = 프로필에 그대로 쓰는 경로).
        private static List<object> NodesJson(TestSetupReport report, NodeTree tree, NodeContentData nodes)
        {
            var list = new List<object>();

            if (!report.Succeeded)
                return list;

            foreach (NodeDefinition node in tree.Nodes)
            {
                int owned = report.Progress.RankOf(node.Id);

                if (owned <= 0)
                    continue;

                var ranks = new List<object>();

                for (int rank = 1; rank <= Math.Min(owned + 1, node.MaxRank); rank++)
                {
                    var values = new List<object>();

                    foreach (string path in BalanceProfilePatcher.NodePaths(nodes, node.Id, rank))
                    {
                        if (!BalanceProfilePatcher.TryRead(path, null, nodes, out double value, out _))
                            continue;

                        var entry = new JsonObject { { "path", path }, { "value", value } };
                        string statId = path.Substring(path.LastIndexOf('/') + 1);
                        NodeEffectRowData effect = statId == "cost"
                            ? null
                            : nodes.Effects.Find(e => e.NodeId == node.Id && e.Rank == rank && e.StatId == statId);

                        if (effect != null)
                            entry.Add("unit", effect.Unit);

                        values.Add(entry);
                    }

                    ranks.Add(new JsonObject { { "rank", rank }, { "owned", rank <= owned }, { "values", values } });
                }

                list.Add(new JsonObject { { "id", node.Id }, { "rank", owned }, { "maxRank", node.MaxRank }, { "ranks", ranks } });
            }

            return list;
        }

        // 노드가 아닌 모든 경로의 지금 원본 값.
        private static JsonObject ValuesJson(ContentData data, NodeContentData nodes)
        {
            var values = new JsonObject();

            foreach (string path in BalanceProfilePatcher.ContentPaths(data))
            {
                if (BalanceProfilePatcher.TryRead(path, data, nodes, out double value, out _))
                    values.Add(path, value);
            }

            return values;
        }

        private static JsonObject DraftJson(AiContextInput input, TestSetupReport baseReport)
        {
            BalanceProfile draft = input.Draft;
            var patches = new List<object>();

            foreach (BalanceProfile.Patch patch in draft.patches)
            {
                if (patch == null)
                    continue;

                var entry = new JsonObject { { "path", patch.path } };

                if (BalanceProfilePatcher.TryRead(patch.path, input.BaseData, input.BaseNodes, out double before, out string readError))
                {
                    entry.Add("before", before);
                    entry.Add("after", patch.value);
                    entry.Add("ratio", before != 0 ? (object)Math.Round(patch.value / before, 3) : null);
                }
                else
                {
                    entry.Add("before", null);
                    entry.Add("after", patch.value);
                    entry.Add("error", readError);
                }

                entry.Add("target", ProfileTargets.TryMap(patch.path, out ProfileTarget target, out string mapError)
                    ? target.ToString()
                    : "승격 안 됨: " + mapError);
                entry.Add("reason", patch.reason);
                entry.Add("noteIds", new List<object>(patch.noteIds ?? new List<string>()));
                patches.Add(entry);
            }

            bool valid = input.DraftErrors.Count == 0 && input.DraftContent != null;
            var json = new JsonObject
            {
                { "name", draft.name },
                { "note", draft.note },
                { "valid", valid },
                { "errors", new List<object>(input.DraftErrors) },
                { "fingerprint", valid ? input.DraftFingerprint : null },
                { "patches", patches },
            };

            if (!valid)
                return json;

            TestSetupReport report = TestSetupPreview.Build(input.Setup, input.DraftContent, input.DraftTree);
            json.Add("compare", Compare(baseReport, report));
            json.Add("simulation", PlaytestSimulation.Run(input.Setup, input.DraftContent, input.DraftTree).ToJson());
            return json;
        }

        // 확정 정보에서 원본과 초안이 다른 칸만.
        private static List<object> Compare(TestSetupReport original, TestSetupReport draft)
        {
            var before = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TestSetupReport.Section section in original.Sections)
            {
                foreach ((string label, string value) in section.Rows)
                    before[section.Title + "\n" + label] = value;
            }

            var list = new List<object>();
            foreach (TestSetupReport.Section section in draft.Sections)
            {
                foreach ((string label, string value) in section.Rows)
                {
                    before.TryGetValue(section.Title + "\n" + label, out string old);

                    if (old != value)
                        list.Add(new JsonObject { { "section", section.Title }, { "label", label }, { "base", old }, { "draft", value } });
                }
            }

            return list;
        }

        private static List<object> Take(List<JsonObject> items, int max)
        {
            var list = new List<object>();
            for (int i = 0; i < items.Count && i < max; i++)
                list.Add(items[i]);

            return list;
        }
    }
}
#endif
