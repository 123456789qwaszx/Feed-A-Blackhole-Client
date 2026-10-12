using System;
using System.Collections.Generic;

namespace BlackHole.Unity
{
    // 사용자 설정 중 UI 표현에 들어가는 것(언어, 글자 크기)을 UI에 넣는다. 시작할 때 한 번, 설정이 바뀔 때마다.
    // - UIContext: 테마·언어·글자 배율을 담아 UIManager에 바꿔 끼운다 → 보이는 화면을 다시 적용한다.
    //   TextBindings에 역할로 등록된 글자는 Theme가 "역할 크기 × 배율"로 맞춘다. 숨은 화면은 다음에 열 때 맞춰진다.
    // - TextBindings가 닿지 않는 글자: 등록된 화면 중 ITextScaleView가 제 글자에 배율을 넣는다.
    // 글자 내용(번역)은 Localizer가 따로 맡는다.
    internal sealed class UISettingsSync : IDisposable
    {
        private readonly UIManager _ui;
        private readonly IReadOnlyList<UIBase> _views;
        private readonly GameSettings _settings;

        public UISettingsSync(UIManager ui, IReadOnlyList<UIBase> views, GameSettings settings)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _views = views ?? Array.Empty<UIBase>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _settings.Changed += HandleSettingChanged;
            ApplyTextScale();
        }

        public void Dispose() => _settings.Changed -= HandleSettingChanged;

        // 시작할 때의 UIContext. UIManager를 만들 때 쓴다.
        public static UIContext ContextFor(UITheme theme, GameSettings settings) =>
            new(theme.ToId(), settings.LanguageLocale.ToId(), textScale: settings.TextScale);

        private void HandleSettingChanged(string id)
        {
            if (id != GameSettings.TextSize && id != GameSettings.Language)
                return;

            UIContext current = _ui.Context;
            _ui.SetContext(current.With(current.ThemeId, _settings.LanguageLocale.ToId(), _settings.TextScale));

            if (id == GameSettings.TextSize)
                ApplyTextScale();
        }

        private void ApplyTextScale()
        {
            float scale = _settings.TextScale;

            foreach (UIBase view in _views)
            {
                if (view is ITextScaleView scaled)
                    scaled.ApplyTextScale(scale);
            }
        }
    }
}
