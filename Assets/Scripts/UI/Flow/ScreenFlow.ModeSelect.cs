using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private const string NormalModeId = "normal";

        private static readonly ModeSelectPanel.ModeItem[] Modes =
        {
            new ModeSelectPanel.ModeItem(
                NormalModeId,
                "Normal Mode",
                "The main mode. Break asteroids, planets and stars and feed their matter to the black hole.",
                new Color(0.62f, 0.33f, 0.35f)),
        };

        // 지금 루트(타이틀) 위에 모드 선택 창을 쌓는다. 이미 쌓여 있으면 그 창까지 되돌아간다.
        private void OpenModeSelect()
        {
            _ui.PushPanel<ModeSelectPanel>(
                _modeSelectPresentation,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Build(Modes, NormalModeId);
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

        private void HandleModeSelectNewGameClicked(string modeId) => EnterMode();
        private void HandleModeSelectContinueClicked(string modeId) => EnterMode();

        private void HandleModeSelectBackClicked() => _ui.PopPanel(Unbind);

        // 루트를 바꿔도 패널은 남는다. 패널을 먼저 모두 닫고(바인딩 해제) 업그레이드 화면으로 간다.
        private void EnterMode()
        {
            _ui.PopAllPanels(Unbind);
            GoToUpgrade();
        }
    }
}
