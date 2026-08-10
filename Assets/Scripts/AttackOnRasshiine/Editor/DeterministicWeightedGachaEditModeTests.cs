using System;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Domain;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class DeterministicWeightedGachaEditModeTests
    {
        [Test]
        public void SameSeedAndPoolProduceSameDrawRegardlessOfCatalogOrder()
        {
            var firstOrder = new[]
            {
                Entry("weapon:rare", GachaPoolKind.Weapon, 15, 25),
                Entry("weapon:common", GachaPoolKind.Weapon, 85, 5)
            };
            var reverseOrder = new[] { firstOrder[1], firstOrder[0] };

            var first = DeterministicWeightedGacha.Draw(firstOrder, GachaPoolKind.Weapon, Array.Empty<string>(), 123456UL);
            var replay = DeterministicWeightedGacha.Draw(reverseOrder, GachaPoolKind.Weapon, Array.Empty<string>(), 123456UL);

            Assert.AreEqual(first.ItemId, replay.ItemId);
            Assert.AreEqual(first.RandomValue, replay.RandomValue);
            Assert.AreEqual(first.Ticket, replay.Ticket);
        }

        [Test]
        public void WeightedBoundariesAreStableAndInclusiveAtTheStart()
        {
            var pool = new[]
            {
                Entry("cosmetic:a", GachaPoolKind.Cosmetic, 70, 5),
                Entry("cosmetic:b", GachaPoolKind.Cosmetic, 30, 20)
            };

            var finalA = DeterministicWeightedGacha.DrawFromRandomValue(pool, GachaPoolKind.Cosmetic, null, 69UL);
            var firstB = DeterministicWeightedGacha.DrawFromRandomValue(pool, GachaPoolKind.Cosmetic, null, 70UL);

            Assert.AreEqual("cosmetic:a", finalA.ItemId);
            Assert.AreEqual("cosmetic:b", firstB.ItemId);
            Assert.AreEqual(100, firstB.TotalWeight);
        }

        [Test]
        public void WeaponAndCosmeticPoolsAreSelectedIndependently()
        {
            var catalog = new[]
            {
                Entry("weapon:blade", GachaPoolKind.Weapon, 1, 10),
                Entry("cosmetic:hat", GachaPoolKind.Cosmetic, 1, 3)
            };

            var weapon = DeterministicWeightedGacha.DrawFromRandomValue(catalog, GachaPoolKind.Weapon, null, 0UL);
            var cosmetic = DeterministicWeightedGacha.DrawFromRandomValue(catalog, GachaPoolKind.Cosmetic, null, 0UL);

            Assert.AreEqual("weapon:blade", weapon.ItemId);
            Assert.AreEqual("cosmetic:hat", cosmetic.ItemId);
        }

        [Test]
        public void DuplicateDrawReturnsConfiguredCompensationOnly()
        {
            var pool = new[] { Entry("weapon:blade", GachaPoolKind.Weapon, 1, 12) };

            var newItem = DeterministicWeightedGacha.Draw(pool, GachaPoolKind.Weapon, Array.Empty<string>(), 7UL);
            var duplicate = DeterministicWeightedGacha.Draw(pool, GachaPoolKind.Weapon, new[] { "weapon:blade" }, 7UL);

            Assert.IsFalse(newItem.IsDuplicate);
            Assert.AreEqual(0, newItem.Compensation);
            Assert.IsTrue(duplicate.IsDuplicate);
            Assert.AreEqual(12, duplicate.Compensation);
        }

        [Test]
        public void EmptyAndAmbiguousPoolsAreRejected()
        {
            Assert.Throws<InvalidOperationException>(() =>
                DeterministicWeightedGacha.Draw(Array.Empty<WeightedGachaEntry>(), GachaPoolKind.Weapon, null, 0UL));

            var duplicates = new List<WeightedGachaEntry>
            {
                Entry("weapon:same", GachaPoolKind.Weapon, 10, 1),
                Entry("weapon:same", GachaPoolKind.Weapon, 20, 2)
            };
            Assert.Throws<InvalidOperationException>(() =>
                DeterministicWeightedGacha.Draw(duplicates, GachaPoolKind.Weapon, null, 0UL));
        }

        private static WeightedGachaEntry Entry(string itemId, GachaPoolKind poolKind, int weight, int compensation)
        {
            return new WeightedGachaEntry(itemId, poolKind, weight, compensation);
        }
    }
}
