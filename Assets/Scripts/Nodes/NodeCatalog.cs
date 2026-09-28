using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 게임에 등록된 노드 전체의 목록:
    // - 노드마다 ID,
    // - 가격,
    // - 시작 노드인가,
    // - 격자 칸,
    // - 이어진 노드,
    // - 구매 시 업그레이드.
    [CreateAssetMenu(fileName = "NodeCatalog", menuName = "BlackHole/Node Catalog")]
    public sealed class NodeCatalog : ScriptableObject
    {
        [SerializeField] private NodeTreeData tree = new();

        // 노드 도구(메뉴 BlackHole > Node Tree)가 고치는 원본. 게임 코드는 ToData()로 읽는다.
        // 노드의 칸·선·시작 노드는 노드 도구가, 가격·업그레이드는 데이터 시트(Nodes·NodeUpgrades 탭)가 원본이다.
        internal NodeTreeData Tree => tree;

        // 데이터 시트 가져오기(메뉴 BlackHole > Data Sheets)만 부른다. 같은 ID의 노드에 가격·업그레이드만 옮긴다.
        internal void ReplaceNumbers(NodeTreeData numbers)
        {
            var byId = new Dictionary<string, NodeData>();

            foreach (NodeData node in numbers.Nodes)
                byId[node.Id] = node;

            foreach (NodeData node in tree.Nodes)
            {
                if (!byId.TryGetValue(node.Id, out NodeData source))
                    continue;

                node.Price = source.Price;
                node.Upgrades = new List<UpgradeData>(source.Upgrades);
            }
        }

        public NodeTreeData ToData() =>
            new NodeTreeData
            {
                Nodes = new List<NodeData>(tree.Nodes)
            };
    }
}
