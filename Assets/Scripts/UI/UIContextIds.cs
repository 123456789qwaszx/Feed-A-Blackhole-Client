using System;

namespace BlackHole.Unity
{
    // UI 테마. UIContext.ThemeId(Presentation 변형 조건이 비교하는 글자)로 바꿔 쓴다.
    public enum UITheme
    {
        Light,
    }

    // UI 언어. UIContext.LocaleId(BCP 47 글자)로 바꿔 쓴다.
    public enum UILocale
    {
        Korean,
    }

    // UI 테마·언어 → UIPresentationFlow가 쓰는 글자.
    public static class UIContextIds
    {
        public static string ToId(this UITheme theme) => theme switch
        {
            UITheme.Light => "Light",
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
        };

        public static string ToId(this UILocale locale) => locale switch
        {
            UILocale.Korean => "ko-KR",
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, null),
        };
    }
}
