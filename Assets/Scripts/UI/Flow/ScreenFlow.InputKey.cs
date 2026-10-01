namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private bool _pauseOpen; // Pause 패널이 열려있는가

        /// <summary>
        /// Space: 결산 화면에서 Upgrade
        /// </summary>
        public void HandleKeyActionShift()
        {
            if (_ui.CurrentRoot is SettlementScreen) GoToUpgrade();
        }

        /// <summary>
        /// 결산 화면, 노드 트리에서 Continue
        /// </summary>
        public void HandleKeyActionSpace()
        {
            if (_ui.CurrentRoot is SettlementScreen
                || _ui.CurrentRoot is UpgradeScreen) RequestStart();
        }

        /// <summary>
        /// 게임 화면에서 Pause
        /// </summary>
        public void HandleKeyActionEsc()
        {
            if (!(_ui.CurrentRoot is BattleScreen)) return;

            if (_pauseOpen) HandlePauseResumeClicked();
            else OpenPause();
        }
    }
}
