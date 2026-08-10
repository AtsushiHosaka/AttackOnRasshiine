using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    /// <summary>
    /// Keeps generated Waypoint Terrace artwork deterministic across fresh clones.
    /// The two horizontal HUD frames are prepared for optional 9-slicing while
    /// emblem/ring/compass artwork remains full-resolution single sprites.
    /// </summary>
    public sealed class WaypointUiAssetPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Art/WaypointUI/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = !assetPath.EndsWith("ui_panel_parchment.png", System.StringComparison.Ordinal);
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 1024;

            if (assetPath.EndsWith("ui_player_status_frame.png", System.StringComparison.Ordinal))
            {
                importer.spriteBorder = new Vector4(168f, 48f, 96f, 48f);
            }
            else if (assetPath.EndsWith("ui_next_raid_frame.png", System.StringComparison.Ordinal))
            {
                importer.spriteBorder = new Vector4(176f, 48f, 88f, 48f);
            }
            else
            {
                importer.spriteBorder = Vector4.zero;
            }
        }
    }
}
