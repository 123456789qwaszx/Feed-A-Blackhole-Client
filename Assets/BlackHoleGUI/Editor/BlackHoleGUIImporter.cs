#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Only changes PNG files inside a folder named BlackHoleGUI/PNG.
public sealed class BlackHoleGUIImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/BlackHoleGUI/PNG/") || !assetPath.EndsWith(".png")) return;
        var texture = (TextureImporter)assetImporter;
        texture.textureType = TextureImporterType.Sprite;
        texture.spriteImportMode = SpriteImportMode.Single;
        texture.alphaIsTransparency = true;
        texture.mipmapEnabled = false;
        texture.sRGBTexture = true;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.maxTextureSize = 1024;
        texture.spritePixelsPerUnit = 100;
        var settings = new TextureImporterSettings();
        texture.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        texture.SetTextureSettings(settings);
        int border = 0;
        if (assetPath.Contains("/Buttons/rect_") || assetPath.EndsWith("/tooltip.png") || assetPath.Contains("/slider_track.png") || assetPath.Contains("/slider_fill.png")) border = 24;
        if (assetPath.EndsWith("/panel.png")) border = 28;
        texture.spriteBorder = new Vector4(border, border, border, border);
    }

    [MenuItem("Tools/Black Hole GUI/Reimport PNG Sprites")]
    public static void Reimport()
    {
        foreach (string path in AssetDatabase.GetAllAssetPaths())
            if (path.Contains("/BlackHoleGUI/PNG/") && path.EndsWith(".png"))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
#endif
