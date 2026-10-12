using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/ThemeSpec")]
public class ThemeSpec : ScriptableObject
{
    [Header("Base Colors")]
    public Color textMainColor = Color.white;
    public Color textWeakColor = Color.gray;

    [Header("Typography")]
    // Fonts by locale and role, shared by every screen's theme. Empty = keep the authored fonts.
    public UIFontSet fonts;
    public int titleSize = 28;
    public int bodySize = 16;
    public int captionSize = 13;

    // textScale: user text size multiplier applied to the role sizes (UIContext.TextScale).
    // localeId: picks the locale row of the font set (UIContext.LocaleId).
    public void BuildPatches(
        List<IUIPatch> patches,
        UITextBindingCatalog textBindings,
        float textScale = 1f,
        string localeId = null)
    {
        if (textBindings == null)
            return;

        patches.Add(new ThemeSpecPatch(this, textBindings, textScale, localeId));
    }
}
