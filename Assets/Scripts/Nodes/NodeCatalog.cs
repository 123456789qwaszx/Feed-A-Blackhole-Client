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

        public NodeTreeData ToData() =>
            new NodeTreeData
            {
                Nodes = new List<NodeData>(tree.Nodes)
            };
    }
}
