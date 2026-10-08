namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private bool _pauseOpen; // Pause 패널이 열려있는가
        private bool _settingsOpen; // Setting 패널이 열려있는가?

        /// <summary>
        /// Shift: 결산 화면에서 Upgrade
        /// </summary>
        private void HandleKeyActionShift()
        {
            if (_ui.CurrentRoot is SettlementScreen) GoToUpgrade();
        }

        /// <summary>
        /// 결산 화면, 노드 트리에서 Continue
        /// </summary>
        private void HandleKeyActionSpace()
        {
            if (_ui.CurrentRoot is SettlementScreen
                || _ui.CurrentRoot is UpgradeScreen) RequestStart();
        }

        /// <summary>
        /// 게임 화면에서 Pause
        /// </summary>
        private void HandleKeyActionEsc()
        {
            if (!(_ui.CurrentRoot is BattleScreen)) return;

            if (_settingsOpen) return; // 설정 창이 열려 있으면 ESC를 무시한다

            if (_pauseOpen) HandlePauseResumeClicked();
            else OpenPause();
        }
    }
}
