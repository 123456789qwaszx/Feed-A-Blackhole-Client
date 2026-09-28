using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 끝난 판의 원자료와 결산을 마친 진행 상태를 보여 준다(성장도는 이미 올라 있다).
        public void GoToSettlement(BattleRawData raw)
        {
            _ui.SwitchRoot<SettlementScreen>(
                _settlementPresentation,
                afterPresented: root =>
                {
                    BindView(root, ApplyBindings);
                    root.ShowResult(raw.PlayedSeconds, raw.ReachedLevel, raw.Stage, raw.NextStage, raw.ReachedMilestone);
                    root.ShowKills(raw.TotalKills, raw.Kills);
                    root.ShowGold(raw.EarnedGold, raw.SettledGold, raw.ReachedMilestone, _player.Gold);
                    root.ShowProgress(_growth.MilestonesReachedBy(_player.GrowthStage), _growth.Milestones.Count);
                },
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
