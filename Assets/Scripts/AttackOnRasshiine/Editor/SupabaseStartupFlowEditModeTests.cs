using System.Reflection;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseStartupFlowEditModeTests
    {
        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        public void StartupSnapshotIsOnlyRequiredForSessionRestoreOrFrontDisplay(
            bool hasSession,
            bool isFrontDisplayScene,
            bool expected)
        {
            var method = typeof(RaidGameApp).GetMethod(
                "RequiresStartupSnapshot",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            var actual = (bool)method.Invoke(null, new object[] { hasSession, isFrontDisplayScene });
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
