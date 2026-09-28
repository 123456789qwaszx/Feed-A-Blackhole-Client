using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/ThemeSpec")]
public class ThemeSpec : ScriptableObject
{
    [Header("Base Colors")]
    public Color textMainColor = Color.white;
    public Color textWeakColor = Color.gray;

    [Header("Typography")]
    public TMP_FontAsset mainFont;
    public int titleSize = 28;
    public int bodySize = 16;
    public int captionSize = 13;

    public void BuildPatches(
        List<IUIPatch> patches,
        UITextBindingCatalog textBindings)
    {
        if (textBindings == null)
            return;

        patches.Add(new ThemeSpecPatch(this, textBindings));
    }
}
