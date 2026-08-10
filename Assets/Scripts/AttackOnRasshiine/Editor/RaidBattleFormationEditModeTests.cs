using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Scene;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidBattleFormationEditModeTests
    {
        [Test]
        public void ThreeVisibleParticipantsSpreadAcrossTwoLeftForegroundDepths()
        {
            var offsets = Enumerable.Range(0, 3).Select(index => CalculateParticipantFormationOffset(index, 3)).ToList();

            Assert.That(offsets.Min(offset => offset.x), Is.InRange(-9.85f, -9.75f));
            Assert.That(offsets.Max(offset => offset.x), Is.InRange(-4.85f, -4.75f));
            Assert.That(offsets.Max(offset => offset.x) - offsets.Min(offset => offset.x), Is.GreaterThan(4.9f));
            Assert.IsTrue(offsets.All(offset => offset.z < -4.00f), "Members must stay in the lower-left foreground instead of merging into the boss.");
            Assert.That(offsets.Select(offset => offset.x).Distinct().Count(), Is.EqualTo(3));
            Assert.That(offsets.Select(offset => offset.z).Distinct().Count(), Is.EqualTo(2));

            foreach (var offset in offsets)
            {
                Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
            }
        }

        [Test]
        public void SingleParticipantStartsInFrontOfBoss()
        {
            var offset = CalculateParticipantFormationOffset(0, 1);

            Assert.That(offset.x, Is.EqualTo(-3.2f).Within(0.0001f));
            Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.Less(offset.z, -2.5f);
        }

        [Test]
        public void FollowCameraTargetsTheRightSideBossCompositionPoint()
        {
            var method = typeof(RaidFollowCamera).GetMethod(
                "ResolveBossCompositionPoint",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var resolved = (Vector3)method.Invoke(null, new object[] { new Vector3(1f, 0f, 2f) });
            Assert.That(resolved.x, Is.EqualTo(2.85f).Within(0.001f));
            Assert.That(resolved.z, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void FollowCameraPullsBackAndRisesEnoughToRevealTheRearWaterway()
        {
            var distanceField = typeof(RaidFollowCamera).GetField("BattleFollowDistanceFloor", BindingFlags.NonPublic | BindingFlags.Static);
            var heightField = typeof(RaidFollowCamera).GetField("BattleFollowHeightFloor", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(distanceField);
            Assert.NotNull(heightField);

            Assert.That((float)distanceField.GetRawConstantValue(), Is.GreaterThanOrEqualTo(9.4f));
            Assert.That((float)heightField.GetRawConstantValue(), Is.GreaterThanOrEqualTo(5.2f));
        }

        private static Vector3 CalculateParticipantFormationOffset(int index, int count)
        {
            var method = typeof(RaidBattleController).GetMethod("CalculateParticipantFormationOffset", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (Vector3)method.Invoke(null, new object[] { index, count });
        }
    }
}
