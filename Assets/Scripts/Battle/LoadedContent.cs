using BlackHole.Core;

namespace BlackHole.Unity
{
    // 불러온 게임 정의. 판 조립은 Content·NodeTree를, 업그레이드 화면의 격자 칸은 NodeLayout을 쓴다.
    public sealed class LoadedContent
    {
        public GameContent Content { get; }
        public NodeTree NodeTree { get; }
        public NodeTreeData NodeLayout { get; }

        internal LoadedContent(GameContent content, NodeTree nodeTree, NodeTreeData nodeLayout)
        {
            Content = content;
            NodeTree = nodeTree;
            NodeLayout = nodeLayout;
        }
    }
}
