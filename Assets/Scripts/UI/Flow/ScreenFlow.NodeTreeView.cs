using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 노드 툴팁의 설명 자리표시 문구. 노드 데이터(NodeData)가 정해지면 노드 ID로 이름·설명을 채운다.
        // 제목은 그때까지 노드 ID를 쓴다(노드 칸에는 이제 가격만 보인다).
        private const string NodeTooltipBodyStub = "Node description goes here.";

        // UpgradeScreen을 Host로 사용하여, 트리 보기 페이지를 염.
        // UpgradeScreen Panel이 닫히면 NodeTreeViewPage 닫히고 바인딩 해제됨.
        private void SwitchToNodeTreePage(UpgradeScreen host)
        {
            _ui.SwitchPage<NodeTreeView>(
                host,
                _nodeTreePresentation,
                afterPresented: page =>
                {
                    BindView(page, ApplyBindings);
                    page.Build(_nodes, _tree.Graph.Links);
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(NodeTreeView page)
        {
            AddBinding(page,
                p => p.NodeClicked += HandleNodeTreeNodeClicked,
                p => p.NodeClicked -= HandleNodeTreeNodeClicked);

            AddBinding(page,
                p => p.NodeHovered += HandleNodeTreeNodeHovered,
                p => p.NodeHovered -= HandleNodeTreeNodeHovered);

            AddBinding(page,
                p => p.NodeLeft += HandleNodeTreeNodeLeft,
                p => p.NodeLeft -= HandleNodeTreeNodeLeft);
        }

        private void HandleNodeTreeNodeClicked(string id)
        {
            NodePurchase.TryPurchase(_player, _tree, id);
            RefreshUpgrade();
        }

        // 툴팁은 호스트(업그레이드 화면)가 띄운다. 지금은 제목만 노드 ID, 설명은 자리표시 문구다.
        private void HandleNodeTreeNodeHovered(string id, RectTransform node)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.ShowNodeTooltip(node, id, NodeTooltipBodyStub);
        }

        private void HandleNodeTreeNodeLeft(string id)
        {
            if (_ui.CurrentRoot is UpgradeScreen root)
                root.HideNodeTooltip();
        }
    }
}
