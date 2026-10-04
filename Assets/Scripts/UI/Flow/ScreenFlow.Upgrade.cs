using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 업그레이드 화면으로(모드 선택, 결산).
        public void GoToUpgrade() => _transition.Play(ShowUpgrade);

        private void ShowUpgrade()
        {
            _ui.SwitchRoot<UpgradeScreen>(
                _upgradePresentation,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    SwitchToNodeTreePage(root);
                    RefreshUpgrade();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(UpgradeScreen root)
        {
            AddBinding(root,
                r => r.StartBattleClicked += HandleUpgradeStartBattleClicked,
                r => r.StartBattleClicked -= HandleUpgradeStartBattleClicked);
        }

        private void HandleUpgradeStartBattleClicked() => RequestStart();

        // 진행 상태를 화면 값(Gold, 블랙홀 성장도·다음 판의 목표 Level, 노드마다의 상태)으로 바꿔 넘긴다.
        private void RefreshUpgrade()
        {
            if (!(_ui.CurrentRoot is UpgradeScreen root))
                return;

            root.ShowGold(_player.Gold);

            int stage = _player.GrowthStage;
            root.ShowHq(stage, _growth.GoalLevelAt(stage));

            var states = new Dictionary<string, NodeState>(_tree.Nodes.Count, StringComparer.Ordinal);
            foreach (NodeDefinition node in _tree.Nodes)
                states.Add(node.Id, NodePurchase.StateOf(_player, _tree, node.Id));

            _ui.GetUI<NodeTreeView>().Show(states);
        }
    }
}
