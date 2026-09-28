using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        public void GoToBattle()
        {
            _ui.SwitchRoot<BattleScreen>(
                _battlePresentation,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    root.ShowIdle();
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(BattleScreen root)
        {
            AddBinding(root,
                r => r.PauseClicked += HandleBattlePauseClicked,
                r => r.PauseClicked -= HandleBattlePauseClicked);

            AddBinding(root,
                r => r.EndClicked += HandleBattleEndClicked,
                r => r.EndClicked -= HandleBattleEndClicked);
        }

        private void HandleBattlePauseClicked() => _battle.TogglePause();
        private void HandleBattleEndClicked() => RequestEnd();

        internal void HandleBattleTimeExpired() => RequestEnd();

        // 화면 버튼과 시간 종료가 같은 전환 경로를 사용한다.
        private void RequestStart()
        {
            try
            {
                if (_battle.TryStart(NodePurchase.UpgradesFor(_player, _tree)))
                    GoToBattle();
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        private async void RequestEnd()
        {
            try
            {
                BattleRawData raw = await _battle.TryEndAsync();
                if (raw != null)
                    GoToSettlement(raw);
            }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
