using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private const string NormalModeId = "normal";

        // 모드 카드. 계속 버튼은 이어 할 진행이 있을 때만 켠다(진행 저장).
        private ModeSelectPanel.ModeItem[] Modes() => new[]
        {
            new ModeSelectPanel.ModeItem(
                NormalModeId,
                "Normal Mode",
                "The main mode. Break asteroids, planets and stars and feed their matter to the black hole.",
                new Color(0.62f, 0.33f, 0.35f),
                canContinue: _progress.CanContinue),
        };

        // 지금 루트(타이틀) 위에 모드 선택 창을 쌓는다. 이미 쌓여 있으면 그 창까지 되돌아간다.
        private void OpenModeSelect()
        {
            _ui.PushPanel<ModeSelectPanel>(
                _modeSelectPresentation,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Build(Modes(), NormalModeId);
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(ModeSelectPanel panel)
        {
            AddBinding(panel,
                p => p.NewGameClicked += HandleModeSelectNewGameClicked,
                p => p.NewGameClicked -= HandleModeSelectNewGameClicked);

            AddBinding(panel,
                p => p.ContinueClicked += HandleModeSelectContinueClicked,
                p => p.ContinueClicked -= HandleModeSelectContinueClicked);

            AddBinding(panel,
                p => p.BackClicked += HandleModeSelectBackClicked,
                p => p.BackClicked -= HandleModeSelectBackClicked);
        }

        // 새 게임: 이어 할 진행이 있으면 지운다는 것을 확인받는다.
        private void HandleModeSelectNewGameClicked(string modeId)
        {
            if (_progress.CanContinue)
                OpenConfirm("NEW GAME", "Start over from the beginning?\nYour saved progress will be lost.", "START", "CANCEL", StartNewGame);
            else
                StartNewGame();
        }

        // 계속: 저장으로 진행 상태를 되살린다(이번 실행에서 이미 진행 중이면 그대로).
        private void HandleModeSelectContinueClicked(string modeId)
        {
            _progress.Continue(_player);
            EnterMode();
        }

        // 진행 상태를 처음으로 되돌리고 바로 저장한 뒤 들어간다.
        private void StartNewGame()
        {
            _progress.StartNew(_player);
            EnterMode();
        }

        private void HandleModeSelectBackClicked() => _ui.PopPanel(Unbind);

        // 화면이 다 덮인 뒤 패널을 모두 닫고(바인딩 해제) 업그레이드 화면으로 바꾼다. 루트를 바꿔도 패널은 남기 때문이다.
        private void EnterMode() => _transition.Play(ShowUpgradeFromModeSelect);

        private void ShowUpgradeFromModeSelect()
        {
            _ui.PopAllPanels(Unbind);
            ShowUpgrade();
        }
    }
}
