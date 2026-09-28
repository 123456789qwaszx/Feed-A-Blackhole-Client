using System;
using UnityEngine;

[Serializable]
public sealed class UIImageBindingEntry
{
    public string refId;

    // Last authored Sprite observed on the UI View.
    // Sync uses this to distinguish an untouched Theme slot from an override.
    public Sprite baseSprite;

    // Sprite applied by this Image Theme.
    public Sprite sprite;

    // The Ref no longer exists on the scanned View.
    // Kept for review instead of being deleted automatically.
    public bool stale;
}
