using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    public sealed class NodeContent
    {
        private readonly Dictionary<string, UpgradeStatDefinition> _statsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeDefinition> _nodesById = new(StringComparer.Ordinal);

        public IReadOnlyList<UpgradeStatDefinition> Stats { get; }
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
            if (statId is null)
            {
                stat = null;
                return false;
            }

            return _statsById.TryGetValue(statId, out stat);
        }

        public bool TryGetNode(string nodeId, out NodeDefinition node)
        {
            if (nodeId is null)
            {
                node = null;
                return false;
            }

            return _nodesById.TryGetValue(nodeId, out node);
        }
    }
}
