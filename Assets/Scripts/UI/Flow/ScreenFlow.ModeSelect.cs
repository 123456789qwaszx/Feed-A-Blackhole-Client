using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private const string NormalModeId = "normal";

        // 모드 선택 창에 보일 모드. 지금은 일반 모드뿐이다 — 모드를 더하면 카드가 늘어난다.
        // 계속(CanContinue)은 막아 둔다: 진행은 앱을 켤 때 새로 만든 PlayerState뿐이고 저장이 없어 이어 할 진행이 없다.
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

        // 모드는 일반 모드 하나라 ID로 가르지 않는다. 모드가 늘면 여기서 모드별로 판 설정을 고른다.
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
