using NUnit.Framework;
using AttackOnRasshiine.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.EditorTests
{
    public sealed class LoginOption3AssetEditModeTests
    {
        [TestCase(RasshiineTheme.LoginPanelFramePath, true)]
        [TestCase(RasshiineTheme.LoginInputFramePath, true)]
        [TestCase(RasshiineTheme.LoginStatusFramePath, true)]
        [TestCase(RasshiineTheme.LoginDividerPath, false)]
        [TestCase(RasshiineTheme.LoginCtaButtonPath, true)]
        [TestCase(RasshiineTheme.LoginSunRaysPath, false)]
        public void GeneratedLoginChromeUsesAWebGlSafeSpriteBudget(string assetPath, bool expectsSliceBorder)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.That(sprite, Is.Not.Null, $"Missing generated login sprite: {assetPath}");

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(512));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));

            var webGl = importer.GetPlatformTextureSettings("WebGL");
            Assert.That(webGl.overridden, Is.True);
            Assert.That(webGl.maxTextureSize, Is.LessThanOrEqualTo(512));
            Assert.That(sprite.border.sqrMagnitude > 0f, Is.EqualTo(expectsSliceBorder));
        }

        [Test]
        public void OptionThreeChromeContainsNoRuntimeTextOrBakedButtonTexture()
        {
            var themeSource = System.IO.File.ReadAllText("Assets/Scripts/AttackOnRasshiine/Runtime/UI/RasshiineTheme.cs");
            var loginSource = System.IO.File.ReadAllText("Assets/Scripts/AttackOnRasshiine/Runtime/UI/RaidGameApp.cs");

            StringAssert.Contains("LoginPanelFrame", themeSource);
            StringAssert.Contains("LoginInputFrame", themeSource);
            StringAssert.Contains("LoginStatusFrame", themeSource);
            StringAssert.Contains("LoginDivider", themeSource);
            StringAssert.Contains("LoginCtaButton", themeSource);
            StringAssert.Contains("LoginSunRays", themeSource);
            StringAssert.Contains("CreateLoginLiveButton", loginSource);
            StringAssert.Contains("ui.CreateDisplayText", loginSource);
            StringAssert.Contains("RefreshLoginTextGeometry", loginSource);
            StringAssert.Contains("RequestCharactersInTexture", loginSource);
            StringAssert.Contains("text.SetAllDirty()", loginSource);
            StringAssert.DoesNotContain("SetButtonBakedLabel(loginButton", loginSource);
        }
    }
}
