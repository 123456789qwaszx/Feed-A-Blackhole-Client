using UnityEngine;
using UnityEngine.UI;

public sealed class UIImageThemePatch : IUIPatch
{
    private readonly UIImageThemeSpec _theme;

    public UIImageThemePatch(UIImageThemeSpec theme)
    {
        _theme = theme;
    }

    public void Apply(IUIPresentationRefProvider refs)
    {
        if (_theme == null || refs == null)
            return;

        if (!_theme.TryGetBinding(refs.GetType(), out UIImageBindingSet binding)
            || binding?.images == null)
        {
            return;
        }

        string targetName = refs.GetType().Name;

        foreach (UIImageBindingEntry entry in binding.images)
        {
            if (entry == null || entry.stale)
                continue;

            string refId = (entry.refId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(refId))
                continue;

            if (!refs.TryGetImage(refId, out Image image) || image == null)
            {
                Debug.LogWarning(
                    $"[UIImageThemePatch] Image not found for refId='{refId}' on '{targetName}'.");
                continue;
            }

            image.sprite = entry.sprite;
        }
    }
}
