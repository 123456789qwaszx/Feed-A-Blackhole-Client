using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 불러온 노드 콘텐츠: 업그레이드 수치 정의와 노드(Rank마다의 비용·효과).
    // 원본은 시트 4개다: UpgradeStats(수치) · Nodes(노드 ID·Rank 수) · NodeCost(Rank 비용) · NodeEffects(Rank 효과).
    // 로더(NodeContentLoader)가 보장한다:
    // - 수치 ID·노드 ID는 유일하다.
    // - 모든 노드의 모든 Rank에 비용이 하나, 효과가 하나 이상 있다.
    // - 모든 효과의 StatId가 수치 정의에 있고, 정수 수치의 효과 값은 정수다.
    // 배치(칸·선·시작 노드)와 구매 규칙은 모른다.
    public sealed class NodeContent
    {
        private readonly Dictionary<string, UpgradeStatDefinition> _statsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeDefinition> _nodesById = new(StringComparer.Ordinal);

        // UpgradeStats 시트 순서.
        public IReadOnlyList<UpgradeStatDefinition> Stats { get; }
        // Nodes 시트 순서.
        public IReadOnlyList<NodeDefinition> Nodes { get; }

        internal NodeContent(List<UpgradeStatDefinition> stats, List<NodeDefinition> nodes)
        {
            Stats = stats.AsReadOnly();
            Nodes = nodes.AsReadOnly();

            foreach (UpgradeStatDefinition stat in stats)
                _statsById.Add(stat.StatId, stat);

            foreach (NodeDefinition node in nodes)
                _nodesById.Add(node.Id, node);
        }

        public bool TryGetStat(string statId, out UpgradeStatDefinition stat)
        {
            stat = null;
            return statId != null && _statsById.TryGetValue(statId, out stat);
        }

        public bool TryGetNode(string nodeId, out NodeDefinition node)
        {
            node = null;
            return nodeId != null && _nodesById.TryGetValue(nodeId, out node);
        }
    }
}
