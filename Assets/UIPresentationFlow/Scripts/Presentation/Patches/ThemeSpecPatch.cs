using TMPro;
using UnityEngine;

public sealed class ThemeSpecPatch : IUIPatch
{
    private readonly ThemeSpec _theme;
    private readonly UITextBindingCatalog _textBindings;

    public ThemeSpecPatch(
        ThemeSpec theme,
        UITextBindingCatalog textBindings)
    {
        _theme = theme;
        _textBindings = textBindings;
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

            ApplyTextTheme(text, entry.role);
        }
    }

    private void ApplyTextTheme(TMP_Text text, UITextRole role)
    {
        if (_theme.mainFont != null)
            text.font = _theme.mainFont;

        switch (role)
        {
            case UITextRole.Title:
                text.fontSize = _theme.titleSize;
                text.color = _theme.textMainColor;
                break;

            case UITextRole.Body:
                text.fontSize = _theme.bodySize;
                text.color = _theme.textMainColor;
                break;

            case UITextRole.Caption:
                text.fontSize = _theme.captionSize;
                text.color = _theme.textWeakColor;
                break;
        }
    }
}
