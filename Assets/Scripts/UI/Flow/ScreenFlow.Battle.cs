namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 진행 중인 판이 없으므로 비어 있는 표시(ShowIdle)로 연다. 일시정지 버튼은 멈출 전투가 없어 연결하지 않는다.
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
                r => r.EndClicked += HandleBattleEndClicked,
                r => r.EndClicked -= HandleBattleEndClicked);
        }

        private void HandleBattleEndClicked() => GoToSettlement();
    }
}
