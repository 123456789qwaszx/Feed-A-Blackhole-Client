using System;

namespace BlackHole.Unity
{
    // 타이틀 화면. 버튼이 눌렸다는 사실만 알린다. 무엇을 할지는 ScreenFlow가 정한다.
    public sealed class TitleScreen : UIRoot<TitleScreen.Refs>
    {
        public enum Refs
        {
            StartBtn_Button,
            SettingsBtn_Button,
            QuitBtn_Button,
        }

        public event Action StartClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            BindEvent(View.Button(Refs.StartBtn_Button), _ => StartClicked?.Invoke());
            BindEvent(View.Button(Refs.SettingsBtn_Button), _ => SettingsClicked?.Invoke());
            BindEvent(View.Button(Refs.QuitBtn_Button), _ => QuitClicked?.Invoke());
        }
    }
}
