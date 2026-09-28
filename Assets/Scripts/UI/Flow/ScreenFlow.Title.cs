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

        // 설정 버튼은 열 화면이 아직 없어 연결하지 않는다.
        private void ApplyBindings(TitleScreen root)
        {
            AddBinding(root,
                r => r.StartClicked += HandleTitleStartClicked,
                r => r.StartClicked -= HandleTitleStartClicked);

            AddBinding(root,
                r => r.QuitClicked += HandleTitleQuitClicked,
                r => r.QuitClicked -= HandleTitleQuitClicked);
        }

        private void HandleTitleStartClicked() => GoToUpgrade();

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
