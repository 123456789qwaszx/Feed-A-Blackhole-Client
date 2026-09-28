using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 노드 트리의 저작 형식. 검증 전 값이며 실행에 쓰지 않는다
    // (오직 NodeTreeLoader만이 읽음)
    //
    // Unity 쪽 노드 목록 에셋(NodeCatalog)이 이 형식을 그대로 담고, 노드 도구를 사용해 기획 데이터 작성.
    [Serializable]
    public sealed class NodeTreeData
    {
        public List<NodeData> Nodes = new();
    }
}
