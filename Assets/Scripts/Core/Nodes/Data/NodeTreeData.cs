using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 트리의 저작 형식. 검증 전 값이며 실행에 쓰지 않는다 — NodeTreeLoader만 읽는다.
    // Unity 쪽 노드 목록 에셋(NodeCatalog)이 이 형식을 그대로 담고, 노드 도구가 이 형식을 고친다.
    // 격자 칸(X, Y)은 표시용이다. 규칙(NodeTree)은 읽지 않고, 선은 Links에 적힌 것만 쓴다.
    // 이름·아이콘 같은 나머지 표시 칸은 트리 화면(F02)과 함께 붙는다.
    [Serializable]
    public sealed class NodeTreeData
    {
        public List<NodeData> Nodes = new List<NodeData>();
    }
}
