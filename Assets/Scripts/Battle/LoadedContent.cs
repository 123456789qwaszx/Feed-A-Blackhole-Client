using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 불러온 게임 정의. 판 조립과 노드 구매는 Content·NodeTree를, 업그레이드 화면은 NodeItems를 쓴다.
    public sealed class LoadedContent
    {
        public GameContent Content { get; }
        public NodeTree NodeTree { get; }
        // 업그레이드 화면에 그릴 노드(배치 순서): 격자 칸, 첫 가격, 그림을 고르는 스탯, 최대 Rank.
        public IReadOnlyList<NodeItem> NodeItems { get; }

        internal LoadedContent(GameContent content, NodeTree nodeTree, IReadOnlyList<NodeItem> nodeItems)
        {
            Content = content;
            NodeTree = nodeTree;
            NodeItems = nodeItems;
        }
    }
}
