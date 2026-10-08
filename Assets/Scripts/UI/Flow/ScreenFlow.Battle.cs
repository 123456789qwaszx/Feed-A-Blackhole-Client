using System.Threading.Tasks;
using BlackHole.Core;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private void ShowBattle()
        {
            _ui.SwitchRoot<BattleScreen>(
                _presentations.Battle,
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

        private void HandleBattlePauseClicked() => OpenPause();
        private void HandleBattleEndClicked() => RequestEnd();

        internal void HandleBattleTimeExpired() => RequestEnd();

        // 화면 버튼과 시간 종료가 같은 전환 경로를 사용한다. 판은 화면이 다 덮인 뒤에 바꾼다.
        // 덮인 뒤의 일에서 난 예외는 ScreenTransition이 로그로 남기고 전환을 이어 간다.
        // 시작: 덮인 뒤 판을 조립·시작하고 전투 화면으로 바꾼다. 덮이는 동안 판이 흐르지 않는다.
        private void RequestStart()
        {
            SoundManager.Instance.PlaySwitchingScreens();
            _transition.Play(StartBattle);
        }

        // 끝: 덮인 뒤 판을 정리·결산하고 결산 화면으로 바꾼다. 적이 치워지는 모습이 보이지 않는다.
        private void RequestEnd() => _transition.Play(EndBattleAsync);

        private void StartBattle()
        {
            if (_battle.TryStart(NodePurchase.StatsFor(_progress, _tree)))
                ShowBattle();
        }

        private async Task EndBattleAsync()
        {
            BattleRawData raw = await _battle.TryEndAsync();

            if (raw != null)
            {
                // 결산이 진행 상태(Gold·성장도)를 바꿨다.
                _progressStore.Save(_progress);
                ShowSettlement(raw);
            }
        }
    }
}
