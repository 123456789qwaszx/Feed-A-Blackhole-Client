namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
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
    }
}
