using System;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 전투 화면의 Pause 버튼: 판을 멈추고 그 위에 일시 정지 창을 쌓는다.
        // 지금은 이 길로만 들어온다. 판이 끝나 정리 중이면 열지 않는다(곧 결산·타이틀로 화면이 바뀌므로 창이 남는다).
        private void OpenPause()
        {
            if (!_battle.IsRunning)
                return;

            SetBattlePaused(true);

            _ui.PushPanel<PausePanel>(
                _pausePresentation,
                afterPresented: panel => BindView(panel, ApplyBindings),
                afterClosed: Unbind);

            _pauseOpen = true;
        }

        private void ApplyBindings(PausePanel panel)
        {
            AddBinding(panel,
                p => p.ResumeClicked += HandlePauseResumeClicked,
                p => p.ResumeClicked -= HandlePauseResumeClicked);

            AddBinding(panel,
                p => p.SettingsClicked += HandlePauseSettingsClicked,
                p => p.SettingsClicked -= HandlePauseSettingsClicked);

            AddBinding(panel,
                p => p.MainMenuClicked += HandlePauseMainMenuClicked,
                p => p.MainMenuClicked -= HandlePauseMainMenuClicked);

            AddBinding(panel,
                p => p.QuitClicked += HandlePauseQuitClicked,
                p => p.QuitClicked -= HandlePauseQuitClicked);
        }

        // 재개: 창을 닫고 판을 다시 움직인다.
        private void HandlePauseResumeClicked()
        {
            _ui.PopPanel(Unbind);
            SetBattlePaused(false);
            _pauseOpen = false;
        }

        // 설정: 일시 정지 창 위에 설정 창을 쌓는다. 설정 창을 닫으면 일시 정지 창으로 돌아온다.
        private void HandlePauseSettingsClicked() => OpenSettings();

        // 메인 메뉴: 화면이 다 덮인 뒤 창을 모두 닫고, 끝내기 버튼과 같은 정리(결산 포함)를 거쳐 결산 화면 대신 타이틀로 바꾼다.
        private void HandlePauseMainMenuClicked() => _transition.Play(LeaveBattleToTitleAsync);

        private async Task LeaveBattleToTitleAsync()
        {
            _ui.PopAllPanels(Unbind);

            try
            {
                bool raw = await _battle.TryAbandonAsync();

                if (raw)
                    ShowTitle();
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        private static void HandlePauseQuitClicked() => QuitGame();

        // 진행 중인 판을 멈추거나 다시 움직인다. 이미 그 상태면 그대로 둔다.
        private void SetBattlePaused(bool paused)
        {
            if (!_battle.IsRunning || _battle.Session == null)
                return;

            bool isPaused = _battle.Session.Phase == SessionPhase.Paused;
            if (isPaused != paused)
                _battle.TogglePause();
        }
    }
}
