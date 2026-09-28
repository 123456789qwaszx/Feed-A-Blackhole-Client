using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 트리의 그래프: 노드 ID, 시작 노드, 선. "여기까지 어떻게 도달하는가"만 안다.
    // 가격·Gold·업그레이드는 모른다(노드 정의 NodeDefinition과 노드 구매 NodePurchase의 일).
    //
    // 선은 방향이 없고 순환을 허용한다(원작 트리의 격자 묶음은 이웃끼리 모두 이어져 있다).
    // 드러남 = 시작 노드이거나, 가진 노드와 선으로 이어져 있다. 이웃 하나로 충분하다
    // (원작: 사야 그 너머가 드러난다. Path of Exile의 "찍은 노드의 이웃"과 같은 규칙).
    // 누가 무엇을 가졌는지는 묻는 쪽이 넘긴다.
    //
    // 로더가 보장하는 것: 노드 ID가 유일하다, 선은 그래프 안의 다른 노드를 가리킨다,
    // 노드가 있으면 시작 노드가 하나 이상이고 모든 노드가 시작 노드에서 선을 따라 닿는다.
    public sealed class NodeGraph
    {
        private readonly HashSet<string> _starts;
        private readonly Dictionary<string, IReadOnlyList<string>> _neighbors;

        // 노드 ID(저작 순서).
        public IReadOnlyList<string> Nodes { get; }
        // 선 목록. 한 선은 한 번만 나온다. 화면이 선을 그릴 때 읽는다.
        public IReadOnlyList<(string A, string B)> Links { get; }

        internal NodeGraph(List<string> nodes, HashSet<string> starts, Dictionary<string, IReadOnlyList<string>> neighbors)
        {
            Nodes = nodes.AsReadOnly();
            _starts = starts;
            _neighbors = neighbors;

            var links = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in nodes)
            {
                // 저작 순서로 이미 지나간 노드와의 선은 그쪽에서 적었다.
                foreach (string neighbor in neighbors[id])
                {
                    if (seen.Contains(neighbor))
                        links.Add((neighbor, id));
                }

                seen.Add(id);
            }

            Links = links.AsReadOnly();
        }

        public bool Contains(string id) => id != null && _neighbors.ContainsKey(id);

        public bool IsStart(string id) => id != null && _starts.Contains(id);

        public bool IsRevealed(string id, Func<string, bool> owns)
        {
            if (owns == null)
                throw new ArgumentNullException(nameof(owns));

            if (!Contains(id))
                return false;

            if (_starts.Contains(id))
                return true;

            foreach (string neighbor in _neighbors[id])
            {
                if (owns(neighbor))
                    return true;
            }

            return false;
        }

        // 시작 노드에서 선을 따라 닿지 않는 노드(저작 순서). 로더의 연결 검사가 쓴다.
        internal List<string> Unreachable()
        {
            var reached = new HashSet<string>(_starts, StringComparer.Ordinal);
            var frontier = new Queue<string>(_starts);

            while (frontier.Count > 0)
            {
                foreach (string neighbor in _neighbors[frontier.Dequeue()])
                {
                    if (reached.Add(neighbor))
                        frontier.Enqueue(neighbor);
                }
            }

            var unreachable = new List<string>();

            foreach (string id in Nodes)
            {
                if (!reached.Contains(id))
                    unreachable.Add(id);
            }

            return unreachable;
        }
    }
}
