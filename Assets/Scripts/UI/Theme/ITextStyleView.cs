using TMPro;

namespace BlackHole.Unity
{
    // TextBindings(Presentation)가 닿지 않는 글자를 가진 화면. 등록된 화면이면 UISettingsSync가 시작할 때 한 번,
    // 글자 크기나 언어 설정이 바뀔 때마다 부른다(닫힌 화면도). 화면은 제 글자를 역할과 함께 StyledTexts에 넣어 두고 스타일만 넘긴다.
    // TextBindings에 역할로 등록된 글자는 Theme가 크기·글꼴을 맞추므로 여기에 넣지 않는다(크기가 두 번 곱해진다).
    public interface ITextStyleView
    {
        void ApplyTextStyle(in UITextStyle style);
    }

    // 지금 글자 스타일: 글자 크기 배율과 언어별 글꼴. Theme가 TextBindings 글자에 넣는 것과 같은 기준이다.
    public readonly struct UITextStyle
    {
        public readonly float Scale;
        private readonly UIFontSet _fonts;
        private readonly string _localeId;

        public UITextStyle(float scale, UIFontSet fonts, string localeId)
        {
            Scale = scale > 0f ? scale : 1f;
            _fonts = fonts;
            _localeId = localeId;
        }

        // 시작 전 기본값: 배율 1, 글꼴은 프리팹 그대로.
        public static UITextStyle Default => new(1f, null, null);

        // 이 역할의 글꼴. 글꼴 묶음이 없거나 칸이 비면 null(프리팹 글꼴을 쓴다).
        public TMP_FontAsset FontFor(UITextRole role) =>
            _fonts != null ? _fonts.Get(_localeId, role) : null;
    }
}
