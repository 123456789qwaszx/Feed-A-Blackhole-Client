using UnityEngine;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        public void GoToTitle()
        {
            _ui.SwitchRoot<TitleScreen>(
                _titlePresentation,
                afterPresented: root => BindView(root, ApplyBindings),
                afterClosed: Unbind);
        }

        private void ApplyBindings(TitleScreen root)
        {
            AddBinding(root,
                r => r.StartClicked += HandleTitleStartClicked,
                r => r.StartClicked -= HandleTitleStartClicked);

            AddBinding(root,
                r => r.SettingsClicked += HandleTitleSettingsClicked,
                r => r.SettingsClicked -= HandleTitleSettingsClicked);

            AddBinding(root,
                r => r.QuitClicked += HandleTitleQuitClicked,
                r => r.QuitClicked -= HandleTitleQuitClicked);
        }

        // 시작은 타이틀 위에 모드 선택 창을 연다. 모드를 고른 뒤 업그레이드 화면으로 간다.
        private void HandleTitleStartClicked() => OpenModeSelect();

        private void HandleTitleSettingsClicked() => OpenSettings();

        private static void HandleTitleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
