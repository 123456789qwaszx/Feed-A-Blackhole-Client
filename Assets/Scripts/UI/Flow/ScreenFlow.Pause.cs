using System.Threading.Tasks;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
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

        private void HandlePauseResumeClicked()
        {
            _ui.PopPanel(Unbind);
            SetBattlePaused(false);
            _pauseOpen = false;
        }

        // 설정: 일시 정지 창 위에 설정 창을 쌓는다. 설정 창을 닫으면 일시 정지 창으로 돌아온다.
        private void HandlePauseSettingsClicked() => OpenSettings();

        private void HandlePauseMainMenuClicked() => _transition.Play(LeaveBattleToTitleAsync);

        private async Task LeaveBattleToTitleAsync()
        {
            _ui.PopAllPanels(Unbind);
            _pauseOpen = false;
            _settingsOpen = false;

            if (await _battle.TryAbandonAsync())
                ShowTitle();
        }

        private static void HandlePauseQuitClicked() => QuitGame();

        // 판을 멈추거나 다시 움직인다. 판이 없거나 이미 그 상태면 그대로 둔다.
        private void SetBattlePaused(bool paused) => _battle.SetPaused(paused);
    }
}
