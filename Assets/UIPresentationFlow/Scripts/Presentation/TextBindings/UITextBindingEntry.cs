using System;

[Serializable]
public sealed class UITextBindingEntry
{
    public string refId;
    public UITextRole role = UITextRole.Unassigned;
    public bool stale;
}
