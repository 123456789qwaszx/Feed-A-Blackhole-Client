using System.Collections.Generic;

public readonly struct UIContext
{
    public readonly string ThemeId;   // e.g. "Light", "Dark"
    public readonly string LocaleId;  // e.g. "ko-KR", "ja-JP"

    // Active experiment assignments for the current UI context.
    public readonly IReadOnlyDictionary<ExperimentKey, VariantId> Experiments;

    // Debug/QA-only forced variant overrides by Presentation identity.
    // Presentation identity is data identity, not View routing identity.
    public readonly IReadOnlyDictionary<string, VariantId> PresentationOverrides;

    public UIContext(
        string themeId,
        string localeId,
        IReadOnlyDictionary<ExperimentKey, VariantId> experiments = null,
        IReadOnlyDictionary<string, VariantId> presentationOverrides = null)
    {
        ThemeId               = themeId;
        LocaleId              = localeId;
        Experiments           = experiments;
        PresentationOverrides = presentationOverrides;
    }

    public static UIContext Default =>
        new UIContext("Light", "ko-KR");
}