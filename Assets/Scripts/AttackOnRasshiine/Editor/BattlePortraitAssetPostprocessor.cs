using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    /// <summary>
    /// Keeps battle HUD portraits cheap and deterministic for WebGL builds.
    /// </summary>
    public sealed class BattlePortraitAssetPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Art/BattleUI/Portraits/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;

            var webGl = importer.GetPlatformTextureSettings("WebGL");
            webGl.name = "WebGL";
            webGl.overridden = true;
            webGl.maxTextureSize = 512;
            webGl.format = TextureImporterFormat.Automatic;
            webGl.textureCompression = TextureImporterCompression.Compressed;
            webGl.compressionQuality = 60;
            importer.SetPlatformTextureSettings(webGl);
        }
    }
}
