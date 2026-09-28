using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 불러온 노드 트리: 그래프(NodeGraph)와 노드 정의(가격·업그레이드)를 노드 ID로 잇는다. NodeTreeLoader만 만든다.
    // 구매 규칙은 NodePurchase에 있다. 로더는 그래프의 노드와 노드 정의가 같은 ID 묶음이라는 것을 보장한다.
    public sealed class NodeTree
    {
        private readonly Dictionary<string, NodeDefinition> _byId = new Dictionary<string, NodeDefinition>(StringComparer.Ordinal);

        public NodeGraph Graph { get; }
        // 노드 정의(저작 순서).
        public IReadOnlyList<NodeDefinition> Nodes { get; }

        internal NodeTree(NodeGraph graph, List<NodeDefinition> nodes)
        {
            Graph = graph;
            Nodes = nodes.AsReadOnly();

            foreach (NodeDefinition node in nodes)
                _byId.Add(node.Id, node);
        }

        public bool TryGet(string id, out NodeDefinition node)
        {
            node = null;
            return id != null && _byId.TryGetValue(id, out node);
        }
    }
}
