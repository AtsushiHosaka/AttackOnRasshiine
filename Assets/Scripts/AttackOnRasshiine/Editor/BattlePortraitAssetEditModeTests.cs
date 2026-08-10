using System.Collections.Generic;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.EditorTests
{
    public sealed class BattlePortraitAssetEditModeTests
    {
        private static IEnumerable<string> PortraitPaths()
        {
            yield return RasshiineTheme.BattlePortraitHeroBluePath;
            yield return RasshiineTheme.BattlePortraitHeroRedPath;
            yield return RasshiineTheme.BattlePortraitHeroGreenPath;
            yield return RasshiineTheme.BattlePortraitBossCoralBeetlePath;
        }

        [Test]
        public void BattlePortraitConstantsRemainInsideTheProductionPortraitRoot()
        {
            foreach (var path in PortraitPaths())
            {
                StringAssert.StartsWith(RasshiineTheme.BattlePortraitRoot + "/", path);
                StringAssert.EndsWith("_v1.png", path);
            }
        }

        [Test]
        public void GeneratedBattlePortraitsUseAWebGlSafeSpriteBudget()
        {
            foreach (var path in PortraitPaths())
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(512), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);

                var webGl = importer.GetPlatformTextureSettings("WebGL");
                Assert.That(webGl.overridden, Is.True, path);
                Assert.That(webGl.maxTextureSize, Is.LessThanOrEqualTo(512), path);
            }
        }

        [Test]
        public void ThemeEditorResolutionAssignsEveryAvailableBattlePortrait()
        {
            var gameObject = new GameObject("BattlePortraitTheme");
            try
            {
                var theme = gameObject.AddComponent<RasshiineTheme>();
                foreach (var pair in new[]
                         {
                             (RasshiineTheme.BattlePortraitHeroBluePath, theme.BattlePortraitHeroBlue),
                             (RasshiineTheme.BattlePortraitHeroRedPath, theme.BattlePortraitHeroRed),
                             (RasshiineTheme.BattlePortraitHeroGreenPath, theme.BattlePortraitHeroGreen),
                             (RasshiineTheme.BattlePortraitBossCoralBeetlePath, theme.BattlePortraitBossCoralBeetle)
                         })
                {
                    Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(pair.Item1), pair.Item2, pair.Item1);
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
