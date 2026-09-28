public sealed partial class UIManager
{
    private void ApplyPresentation(
        UIBase view,
        UIPresentationSpec spec,
        in DisplayContext display)
    {
        view.EnsureInitialized();

        if (view is not IUIPage)
            SafeAreaUtility.Apply(view, display);

        UIResolveResult result =
            _resolver.Resolve(spec, display);

        _presentationApplier.Apply(view, result);

        LastDisplay = display;
        LastResult = result;
    }
}