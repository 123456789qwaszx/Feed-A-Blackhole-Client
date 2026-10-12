using System;
using System.Collections.Generic;

namespace BlackHole.Unity
{
    // 사용자 설정 중 UI 표현에 들어가는 것(언어, 글자 크기)을 UI에 넣는다. 시작할 때 한 번, 설정이 바뀔 때마다.
    // - UIContext: 테마·언어·글자 배율을 담아 UIManager에 바꿔 끼운다 → 보이는 화면을 다시 적용한다.
    //   TextBindings에 역할로 등록된 글자는 Theme가 "역할 크기 × 배율"과 "언어 × 역할" 글꼴로 맞춘다. 숨은 화면은 다음에 열 때 맞춰진다.
    // - TextBindings가 닿지 않는 글자: 등록된 화면 중 ITextStyleView가 제 글자에 같은 배율·글꼴을 넣는다.
    // 글꼴 묶음(UIFontSet)은 Theme들이 가리키는 것과 같은 에셋이다(GameBootstrap이 확인한다).
    // - Gold 숫자 표기(Number Format): GoldText.Format을 바꾸고 INumberFormatView가 보이는 숫자를 다시 쓴다.
    // 글자 내용(번역)은 Localizer가 따로 맡는다.
    internal sealed class UISettingsSync : IDisposable
    {
        private readonly UIManager _ui;
        private readonly IReadOnlyList<UIBase> _views;
        private readonly GameSettings _settings;
        private readonly UIFontSet _fonts;

        public UISettingsSync(UIManager ui, IReadOnlyList<UIBase> views, GameSettings settings, UIFontSet fonts)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _views = views ?? Array.Empty<UIBase>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _fonts = fonts;

            _settings.Changed += HandleSettingChanged;
            ApplyTextStyle();
            ApplyNumberFormat();
        }

        public void Dispose() => _settings.Changed -= HandleSettingChanged;

        // 시작할 때의 UIContext. UIManager를 만들 때 쓴다.
        public static UIContext ContextFor(UITheme theme, GameSettings settings) =>
            new(theme.ToId(), settings.LanguageLocale.ToId(), textScale: settings.TextScale);

        private void HandleSettingChanged(string id)
        {
            if (id == GameSettings.NumberFormat)
            {
                ApplyNumberFormat();
                return;
            }

            if (id != GameSettings.TextSize && id != GameSettings.Language)
                return;

            UIContext current = _ui.Context;
            _ui.SetContext(current.With(current.ThemeId, _settings.LanguageLocale.ToId(), _settings.TextScale));
            ApplyTextStyle();
        }

        // Gold 숫자 표기: GoldText가 쓰는 표기를 바꾸고, 숫자를 보이는 화면이 마지막 값을 다시 쓴다.
        private void ApplyNumberFormat()
        {
            GoldText.Format = _settings.NumberFormatValue;

            foreach (UIBase view in _views)
            {
                if (view is INumberFormatView numbers)
                    numbers.RefreshNumbers();
            }
        }

        private void ApplyTextStyle()
        {
            var style = new UITextStyle(_settings.TextScale, _fonts, _settings.LanguageLocale.ToId());

            foreach (UIBase view in _views)
            {
                if (view is ITextStyleView styled)
                    styled.ApplyTextStyle(style);
            }
        }
    }
}
