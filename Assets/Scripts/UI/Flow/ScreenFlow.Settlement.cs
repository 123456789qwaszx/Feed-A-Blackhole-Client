namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 결산할 판이 없으므로 결과 값을 넘기지 않는다. 글자는 프리팹에 적힌 그대로다.
        public void GoToSettlement()
        {
            _ui.SwitchRoot<SettlementScreen>(
                _settlementPresentation,
                afterPresented: root => BindView(root, ApplyBindings),
                afterClosed: Unbind);
        }

        private void ApplyBindings(SettlementScreen root)
        {
            AddBinding(root,
                r => r.ContinueClicked += HandleSettlementContinueClicked,
                r => r.ContinueClicked -= HandleSettlementContinueClicked);
        }

        private void HandleSettlementContinueClicked() => GoToUpgrade();
    }
}
