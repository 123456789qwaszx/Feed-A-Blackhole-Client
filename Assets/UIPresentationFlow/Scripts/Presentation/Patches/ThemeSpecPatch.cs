using TMPro;
using UnityEngine;

public sealed class ThemeSpecPatch : IUIPatch
{
    private readonly ThemeSpec _theme;
    private readonly UITextBindingCatalog _textBindings;
    private readonly float _textScale;
    private readonly string _localeId;

    public ThemeSpecPatch(
        ThemeSpec theme,
        UITextBindingCatalog textBindings,
        float textScale = 1f,
        string localeId = null)
    {
        _theme = theme;
        _textBindings = textBindings;
        _textScale = textScale > 0f ? textScale : 1f;
        _localeId = localeId;
    }

    public void Apply(IUIPresentationRefProvider refs)
    {
        if (_theme == null
            || _textBindings == null
            || refs == null)
        {
            return;
        }

        if (!_textBindings.TryGetBinding(
                refs.GetType(),
                out UITextBindingSet binding)
            || binding?.texts == null)
        {
            return;
        }

        string targetName = refs.GetType().Name;

        foreach (UITextBindingEntry entry in binding.texts)
        {
            if (entry == null
                || entry.stale
                || entry.role == UITextRole.Unassigned)
            {
                continue;
            }

            string refId = (entry.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId))
                continue;

            if (!refs.TryGetText(refId, out TMP_Text text) || text == null)
            {
                Debug.LogWarning(
                    $"[ThemeSpecPatch] TMP_Text not found for text refId='{refId}' on '{targetName}'.");
                continue;
            }

            ApplyTextTheme(text, entry.role, entry.fixedSize ? 1f : _textScale, !entry.fixedFont);
        }
    }

    // The size is always computed from the role size, so re-applying never compounds the scale.
    // Auto-sized texts take the size as their maximum: they grow with the scale and shrink to fit their rect.
    // The font comes from the font set by the current locale and the role (fixedFont entries keep theirs).
    private void ApplyTextTheme(TMP_Text text, UITextRole role, float scale, bool setFont)
    {
        if (setFont && _theme.fonts != null)
        {
            TMP_FontAsset font = _theme.fonts.Get(_localeId, role);
            if (font != null)
                text.font = font;
        }

        switch (role)
        {
            case UITextRole.Title:
                SetSize(text, _theme.titleSize * scale);
                text.color = _theme.textMainColor;
                break;

            case UITextRole.Body:
                SetSize(text, _theme.bodySize * scale);
                text.color = _theme.textMainColor;
                break;

            case UITextRole.Caption:
                SetSize(text, _theme.captionSize * scale);
                text.color = _theme.textWeakColor;
                break;
        }
    }

    private static void SetSize(TMP_Text text, float size)
    {
        if (text.enableAutoSizing)
            text.fontSizeMax = size;

        text.fontSize = size;
    }
}
