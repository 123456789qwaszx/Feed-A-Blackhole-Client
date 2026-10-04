using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // NodeContentData(시트 4개의 행) → NodeContent.
    // 행 순서에 뜻을 두지 않는다: 수치는 StatId로, 노드는 NodeId로, Rank는 (NodeId, Rank)로 짝짓는다.
    //
    // 검사. 수치 규칙은 정의 생성자(UpgradeStatDefinition·NodeEffect·NodeRankDefinition·NodeDefinition)를 그대로 부른다.
    // 1. 수치: StatId 유일, ValueType·Unit 이름, Aggregation은 Add뿐, 기본값·하한·상한.
    // 2. 노드: NodeId 유일, Rank 수 1 이상.
    // 3. 비용: Nodes 시트에 있는 노드, Rank는 1 ~ Rank 수, (노드, Rank)마다 하나, 0보다 큼.
    // 4. 효과: 노드·Rank는 비용과 같은 기준, UpgradeStats 시트에 있는 StatId, 단위가 수치의 단위와 같음, 정수 수치면 정수.
    // 5. 빠짐: 모든 노드의 모든 Rank에 비용과 효과가 있음.
    // 오류는 한 번에 모두 모은다. 하나라도 있으면 결과가 없다(부분 통과 금지).
    // 수치 합이 상한을 넘는 것(원작 asteroid.respawnChance 101%)은 오류가 아니다 — 수치를 계산할 때 상한으로 자른다.
    public static class NodeContentLoader
    {
        public const string AddAggregation = "Add";

        public static NodeContentLoadResult Load(NodeContentData data)
        {
            var diagnostics = new List<ContentDiagnostic>();

            if (data == null)
            {
                diagnostics.Add(new ContentDiagnostic(string.Empty, "노드 콘텐츠 데이터가 null이다."));
                return new NodeContentLoadResult(null, diagnostics);
            }

            var statIds = new HashSet<string>(StringComparer.Ordinal);
            var statsById = new Dictionary<string, UpgradeStatDefinition>(StringComparer.Ordinal);
            List<UpgradeStatDefinition> stats = LoadStats(data.Stats ?? new List<UpgradeStatRowData>(), statIds, statsById, diagnostics);

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            List<NodeEntry> nodes = LoadNodes(data.Nodes ?? new List<NodeRowData>(), nodeIds, diagnostics);
            var nodesById = new Dictionary<string, NodeEntry>(StringComparer.Ordinal);

            foreach (NodeEntry node in nodes)
                nodesById.Add(node.Id, node);

            Dictionary<(string, int), (long Cost, string At)> costs =
                LoadCosts(data.Costs ?? new List<NodeCostRowData>(), nodeIds, nodesById, diagnostics);

            var effectRanks = new HashSet<(string, int)>();
            Dictionary<(string, int), List<NodeEffect>> effects =
                LoadEffects(data.Effects ?? new List<NodeEffectRowData>(), nodeIds, nodesById, statIds, statsById, effectRanks, diagnostics);

            List<NodeDefinition> definitions = Assemble(nodes, costs, effects, effectRanks, diagnostics);

            return diagnostics.Count > 0
                ? new NodeContentLoadResult(null, diagnostics)
                : new NodeContentLoadResult(new NodeContent(stats, definitions), diagnostics);
        }

        // 1. 수치. 이름 칸이 틀렸거나 생성자 규칙에 걸린 수치도 ID는 statIds에 넣는다 — 그 수치를 쓰는 효과마다 "없는 StatId"가 겹쳐 나오지 않게.
        private static List<UpgradeStatDefinition> LoadStats(List<UpgradeStatRowData> rows, HashSet<string> statIds,
            Dictionary<string, UpgradeStatDefinition> statsById, List<ContentDiagnostic> into)
        {
            var stats = new List<UpgradeStatDefinition>(rows.Count);
            var firstAt = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < rows.Count; i++)
            {
                UpgradeStatRowData row = rows[i];
                string at = At(NodeContentCsv.StatsTab, row?.Row ?? 0, i);

                if (row == null)
                {
                    into.Add(new ContentDiagnostic(at, "행이 null이다."));
                    continue;
                }

                string id = row.StatId?.Trim();

                if (string.IsNullOrEmpty(id))
                {
                    into.Add(new ContentDiagnostic(at, "StatId가 비어 있다."));
                    continue;
                }

                if (firstAt.TryGetValue(id, out string first))
                {
                    into.Add(new ContentDiagnostic(at, $"StatId '{id}'가 {first}에도 있다."));
                    continue;
                }

                firstAt.Add(id, at);
                statIds.Add(id);
                bool named = true;

                if (!TryName(row.ValueType, out UpgradeStatValueType valueType))
                {
                    into.Add(new ContentDiagnostic(at, $"ValueType은 Float·Int 가운데 하나다. 받은 값: '{row.ValueType}'."));
                    named = false;
                }

                if (!TryName(row.Unit, out UpgradeStatUnit unit))
                {
                    into.Add(new ContentDiagnostic(at, $"Unit은 Flat·Percent 가운데 하나다. 받은 값: '{row.Unit}'."));
                    named = false;
                }

                if (!string.Equals(row.Aggregation?.Trim(), AddAggregation, StringComparison.Ordinal))
                {
                    into.Add(new ContentDiagnostic(at, $"Aggregation은 {AddAggregation}만 쓸 수 있다. 받은 값: '{row.Aggregation}'."));
                    named = false;
                }

                if (!named)
                    continue;

                try
                {
                    var stat = new UpgradeStatDefinition(id, valueType, unit, row.DefaultValue,
                        row.Min ?? float.NegativeInfinity, row.Max ?? float.PositiveInfinity, row.Enabled);
                    stats.Add(stat);
                    statsById.Add(id, stat);
                }
                catch (ArgumentException error)
                {
                    into.Add(new ContentDiagnostic(at, error.Message));
                }
            }

            return stats;
        }

        // 2. 노드. Rank 수가 틀린 노드도 ID는 nodeIds에 넣는다 — 그 노드의 비용·효과 행마다 "없는 NodeId"가 겹쳐 나오지 않게.
        private static List<NodeEntry> LoadNodes(List<NodeRowData> rows, HashSet<string> nodeIds, List<ContentDiagnostic> into)
        {
            var nodes = new List<NodeEntry>(rows.Count);
            var firstAt = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < rows.Count; i++)
            {
                NodeRowData row = rows[i];
                string at = At(NodeContentCsv.NodesTab, row?.Row ?? 0, i);

                if (row == null)
                {
                    into.Add(new ContentDiagnostic(at, "행이 null이다."));
                    continue;
                }

                string id = row.NodeId?.Trim();

                if (string.IsNullOrEmpty(id))
                {
                    into.Add(new ContentDiagnostic(at, "NodeId가 비어 있다."));
                    continue;
                }

                if (firstAt.TryGetValue(id, out string first))
                {
                    into.Add(new ContentDiagnostic(at, $"NodeId '{id}'가 {first}에도 있다."));
                    continue;
                }

                firstAt.Add(id, at);
                nodeIds.Add(id);

                if (row.RankCount < 1)
                {
                    into.Add(new ContentDiagnostic(at, $"'{id}'의 Rank 수는 1 이상이다. 받은 값: {row.RankCount}."));
                    continue;
                }

                nodes.Add(new NodeEntry(id, row.RankCount, row.Memo, at));
            }

            return nodes;
        }

        // 3. 비용.
        private static Dictionary<(string, int), (long Cost, string At)> LoadCosts(List<NodeCostRowData> rows, HashSet<string> nodeIds,
            Dictionary<string, NodeEntry> nodes, List<ContentDiagnostic> into)
        {
            var costs = new Dictionary<(string, int), (long, string)>();

            for (int i = 0; i < rows.Count; i++)
            {
                NodeCostRowData row = rows[i];
                string at = At(NodeContentCsv.CostTab, row?.Row ?? 0, i);

                if (row == null)
                {
                    into.Add(new ContentDiagnostic(at, "행이 null이다."));
                    continue;
                }

                string id = row.NodeId?.Trim();

                if (!IsRankOf(id, row.Rank, at, nodeIds, nodes, into))
                    continue;

                if (costs.TryGetValue((id, row.Rank), out (long Cost, string At) first))
                {
                    into.Add(new ContentDiagnostic(at, $"'{id}' Rank {row.Rank}의 비용이 {first.At}에도 있다."));
                    continue;
                }

                costs.Add((id, row.Rank), (row.Cost, at));
            }

            return costs;
        }

        // 4. 효과. 같은 (노드, Rank)에 효과가 여럿이어도 된다.
        // ranks: 효과 행이 하나라도 가리킨 (노드, Rank). 그 행이 다른 이유로 틀렸어도 넣는다 — 5단계가 "효과 없음"을 겹쳐 알리지 않게.
        private static Dictionary<(string, int), List<NodeEffect>> LoadEffects(List<NodeEffectRowData> rows, HashSet<string> nodeIds,
            Dictionary<string, NodeEntry> nodes, HashSet<string> statIds, Dictionary<string, UpgradeStatDefinition> stats,
            HashSet<(string, int)> ranks, List<ContentDiagnostic> into)
        {
            var effects = new Dictionary<(string, int), List<NodeEffect>>();

            for (int i = 0; i < rows.Count; i++)
            {
                NodeEffectRowData row = rows[i];
                string at = At(NodeContentCsv.EffectsTab, row?.Row ?? 0, i);

                if (row == null)
                {
                    into.Add(new ContentDiagnostic(at, "행이 null이다."));
                    continue;
                }

                string id = row.NodeId?.Trim();

                if (!IsRankOf(id, row.Rank, at, nodeIds, nodes, into))
                    continue;

                ranks.Add((id, row.Rank));
                string statId = row.StatId?.Trim();

                if (string.IsNullOrEmpty(statId))
                {
                    into.Add(new ContentDiagnostic(at, "StatId가 비어 있다."));
                    continue;
                }

                if (!statIds.Contains(statId))
                {
                    into.Add(new ContentDiagnostic(at, $"UpgradeStats 시트에 없는 StatId다: '{statId}'."));
                    continue;
                }

                // 수치 정의 자체가 틀렸으면(1단계에서 이미 알림) 단위·정수 검사는 건너뛴다.
                if (stats.TryGetValue(statId, out UpgradeStatDefinition stat) && !Fits(stat, row, at, into))
                    continue;

                NodeEffect effect;

                try
                {
                    effect = new NodeEffect(statId, row.Value);
                }
                catch (ArgumentException error)
                {
                    into.Add(new ContentDiagnostic(at, error.Message));
                    continue;
                }

                if (!effects.TryGetValue((id, row.Rank), out List<NodeEffect> list))
                    effects.Add((id, row.Rank), list = new List<NodeEffect>());

                list.Add(effect);
            }

            return effects;
        }

        // 5. 노드마다 Rank 1부터 Rank 수까지 비용과 효과를 모아 정의를 만든다. Nodes 시트 순서를 지킨다.
        private static List<NodeDefinition> Assemble(List<NodeEntry> nodes, Dictionary<(string, int), (long Cost, string At)> costs,
            Dictionary<(string, int), List<NodeEffect>> effects, HashSet<(string, int)> effectRanks, List<ContentDiagnostic> into)
        {
            var definitions = new List<NodeDefinition>(nodes.Count);

            foreach (NodeEntry node in nodes)
            {
                var ranks = new List<NodeRankDefinition>(node.RankCount);
                var noCost = new List<int>();
                var noEffect = new List<int>();

                for (int rank = 1; rank <= node.RankCount; rank++)
                {
                    bool hasCost = costs.TryGetValue((node.Id, rank), out (long Cost, string At) cost);
                    bool hasEffects = effects.TryGetValue((node.Id, rank), out List<NodeEffect> list);

                    if (!hasCost)
                        noCost.Add(rank);

                    if (!effectRanks.Contains((node.Id, rank)))
                        noEffect.Add(rank);

                    if (!hasCost || !hasEffects)
                        continue;

                    // 같은 Rank의 효과 순서가 시트 행 순서에 닿지 않게 정렬한다.
                    list.Sort(CompareEffects);

                    try
                    {
                        ranks.Add(new NodeRankDefinition(rank, cost.Cost, list));
                    }
                    catch (ArgumentException error)
                    {
                        into.Add(new ContentDiagnostic(cost.At, error.Message));
                    }
                }

                if (noCost.Count > 0)
                    into.Add(new ContentDiagnostic(node.At, $"'{node.Id}'의 {RankList(noCost)} 비용이 {NodeContentCsv.CostTab} 시트에 없다."));

                if (noEffect.Count > 0)
                    into.Add(new ContentDiagnostic(node.At, $"'{node.Id}'의 {RankList(noEffect)} 효과가 {NodeContentCsv.EffectsTab} 시트에 없다."));

                if (ranks.Count != node.RankCount)
                    continue;

                try
                {
                    definitions.Add(new NodeDefinition(node.Id, node.Memo, ranks));
                }
                catch (ArgumentException error)
                {
                    into.Add(new ContentDiagnostic(node.At, error.Message));
                }
            }

            return definitions;
        }

        // 비용·효과 행이 가리키는 (노드, Rank)가 있는가. Rank 수가 틀린 노드(2단계에서 이미 알림)를 가리키면 조용히 false다.
        private static bool IsRankOf(string id, int rank, string at, HashSet<string> nodeIds, Dictionary<string, NodeEntry> nodes,
            List<ContentDiagnostic> into)
        {
            if (string.IsNullOrEmpty(id))
            {
                into.Add(new ContentDiagnostic(at, "NodeId가 비어 있다."));
                return false;
            }

            if (!nodeIds.Contains(id))
            {
                into.Add(new ContentDiagnostic(at, $"{NodeContentCsv.NodesTab} 시트에 없는 NodeId다: '{id}'."));
                return false;
            }

            if (!nodes.TryGetValue(id, out NodeEntry node))
                return false;

            if (rank < 1 || rank > node.RankCount)
            {
                into.Add(new ContentDiagnostic(at, $"'{id}'의 Rank는 1부터 {node.RankCount}까지다(Nodes 시트 Rank 수). 받은 값: {rank}."));
                return false;
            }

            return true;
        }

        // 효과 값이 수치 정의에 맞는가: 단위가 같고, 정수 수치면 정수다.
        private static bool Fits(UpgradeStatDefinition stat, NodeEffectRowData row, string at, List<ContentDiagnostic> into)
        {
            bool fits = true;

            if (!TryName(row.Unit, out UpgradeStatUnit unit))
            {
                into.Add(new ContentDiagnostic(at, $"단위는 Flat·Percent 가운데 하나다. 받은 값: '{row.Unit}'."));
                fits = false;
            }
            else if (unit != stat.Unit)
            {
                into.Add(new ContentDiagnostic(at, $"단위가 수치 정의와 다르다: '{stat.StatId}'는 {stat.Unit}인데 {unit}로 적었다."));
                fits = false;
            }

            if (!stat.Accepts(row.Value))
            {
                into.Add(new ContentDiagnostic(at, stat.ValueType == UpgradeStatValueType.Int
                    ? $"'{stat.StatId}'는 정수 수치다. 받은 값: {row.Value}."
                    : $"유한한 값이 필요하다. 받은 값: {row.Value}."));
                fits = false;
            }

            return fits;
        }

        // 이름 칸을 enum으로 읽는다. 대소문자까지 같아야 하고, 숫자("0")는 받지 않는다.
        private static bool TryName<T>(string text, out T value) where T : struct, Enum
        {
            value = default;
            string name = text?.Trim();

            if (string.IsNullOrEmpty(name))
                return false;

            foreach (T candidate in (T[])Enum.GetValues(typeof(T)))
            {
                if (string.Equals(candidate.ToString(), name, StringComparison.Ordinal))
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        private static int CompareEffects(NodeEffect a, NodeEffect b)
        {
            int byStat = string.CompareOrdinal(a.StatId, b.StatId);
            return byStat != 0 ? byStat : a.Value.CompareTo(b.Value);
        }

        private static string RankList(List<int> ranks) => "Rank " + string.Join(", ", ranks);

        // 진단 위치: 시트에서 온 행이면 "NodeCost 12행", 아니면 목록 번호.
        private static string At(string tab, int row, int index) => row > 0 ? $"{tab} {row}행" : $"{tab}[{index}]";

        private readonly struct NodeEntry
        {
            public readonly string Id;
            public readonly int RankCount;
            public readonly string Memo;
            public readonly string At;

            public NodeEntry(string id, int rankCount, string memo, string at)
            {
                Id = id;
                RankCount = rankCount;
                Memo = memo;
                At = at;
            }
        }
    }
}
