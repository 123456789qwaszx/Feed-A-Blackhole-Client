using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
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
        }

        private void HandleNodeTreeNodeClicked(string id)
        {
            NodePurchase.TryPurchase(_player, _tree, id);
            RefreshUpgrade();
        }
    }
}
