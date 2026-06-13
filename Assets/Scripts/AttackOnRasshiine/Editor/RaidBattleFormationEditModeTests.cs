using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Battle;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidBattleFormationEditModeTests
    {
        [Test]
        public void ParticipantFormationWrapsAroundBoss()
        {
            var offsets = Enumerable.Range(0, 6).Select(index => CalculateParticipantFormationOffset(index, 6)).ToList();

            Assert.IsTrue(offsets.Any(offset => offset.x < -1f), "Formation should include members on the left side of the boss.");
            Assert.IsTrue(offsets.Any(offset => offset.x > 1f), "Formation should include members on the right side of the boss.");
            Assert.IsTrue(offsets.Any(offset => offset.z < -1f), "Formation should include members in front of the boss.");
            Assert.IsTrue(offsets.Any(offset => offset.z > 1f), "Formation should include members behind the boss.");

            foreach (var offset in offsets)
            {
                Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
                var radius = new Vector2(offset.x, offset.z).magnitude;
                Assert.That(radius, Is.InRange(2.65f, 3.1f));
            }
        }

        [Test]
        public void SingleParticipantStartsInFrontOfBoss()
        {
            var offset = CalculateParticipantFormationOffset(0, 1);

            Assert.That(offset.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.Less(offset.z, -2.5f);
        }

        private static Vector3 CalculateParticipantFormationOffset(int index, int count)
        {
            var method = typeof(RaidBattleController).GetMethod("CalculateParticipantFormationOffset", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (Vector3)method.Invoke(null, new object[] { index, count });
        }
    }
}
