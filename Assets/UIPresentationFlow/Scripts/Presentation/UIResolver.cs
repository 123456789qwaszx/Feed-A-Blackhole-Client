using System.Collections.Generic;

public sealed class UIResolveTrace
{
    private readonly List<string> _lines = new();
    public void Add(string line) => _lines.Add(line);
    public string Dump() => _lines.Count == 0 
        ? "[Trace] (empty)" 
        : "[Trace]\n- " + string.Join("\n- ", _lines);
}

public sealed class UIResolver
{
    private readonly UIVariantResolver _variantResolver = new();
    private readonly UIContext _context;

    public UIResolver(UIContext context)
    {
        _context = context;
    }

    public UIResolveResult Resolve(
        UIPresentationSpec spec,
        in DisplayContext display)
    {
        var trace = new UIResolveTrace();
        ResolvedUIPresentation resolved =
            _variantResolver.Resolve(spec, _context, display, trace);

        var patches = new List<IUIPatch>(3);
        resolved.Theme?.BuildPatches(
            patches,
            resolved.TextBindings);
        resolved.Layout?.BuildPatches(patches);
        resolved.ImageTheme?.BuildPatches(patches);
        trace.Add($"[Patches] {patches.Count}");

        return new UIResolveResult(resolved, patches, trace);
    }
}
