using System.IO;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class ExternalUrlLauncherEditModeTests
    {
        [Test]
        public void ExternalProductLinksUseTheNoopenerWebGlBridge()
        {
            var launcher = File.ReadAllText(
                "Assets/Scripts/AttackOnRasshiine/Runtime/Services/ExternalUrlLauncher.cs");
            var bridge = File.ReadAllText("Assets/Plugins/AorExternalUrl.jslib");
            var app = File.ReadAllText(
                "Assets/Scripts/AttackOnRasshiine/Runtime/UI/RaidGameApp.cs");

            StringAssert.Contains("UNITY_WEBGL && !UNITY_EDITOR", launcher);
            StringAssert.Contains("AOR_OpenExternalUrlNoOpener", launcher);
            StringAssert.Contains("noopener,noreferrer", bridge);
            StringAssert.Contains("opened.opener = null", bridge);
            StringAssert.Contains("ExternalUrlLauncher.TryOpen(normalizedProductUrl)", app);
            StringAssert.DoesNotContain("Application.OpenURL(normalizedProductUrl)", app);
        }

        [TestCase("http://example.com")]
        [TestCase("https://localhost/path")]
        [TestCase("https://user:password@example.com/path")]
        [TestCase("javascript:alert(1)")]
        public void ExternalProductLinkBoundaryRejectsUnsafeDestinations(string value)
        {
            Assert.IsFalse(RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl(value, out _));
        }
    }
}
