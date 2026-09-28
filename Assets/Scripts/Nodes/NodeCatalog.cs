using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 게임에 등록된 노드 전체의 목록: 노드마다 ID, 가격, 시작 노드인가, 격자 칸, 이어진 노드, 사면 받는 업그레이드.
    // Core 저작 형식(NodeTreeData)을 그대로 담는다. 검증(ID 중복, 선, 시작 노드에서 닿는가)은 NodeTreeLoader가 경로와 함께 보고한다.
    // 원본의 노드 도구(메뉴 BlackHole > Node Tree)는 아직 옮기지 않았다 — 지금은 인스펙터에서 고친다.
    // Breaker 노드(breaker.*)는 [임시] 샘플이다. 적 종류 노드(enemy.<종류>.*)는 feature/처치보상의 노드 목록을 옮긴 것이다.
    [CreateAssetMenu(fileName = "NodeCatalog", menuName = "BlackHole/Node Catalog")]
    public sealed class NodeCatalog : ScriptableObject
    {
        [SerializeField] private NodeTreeData tree = new NodeTreeData();

        public NodeTreeData ToData() => new NodeTreeData { Nodes = new List<NodeData>(tree.Nodes) };
    }
}
