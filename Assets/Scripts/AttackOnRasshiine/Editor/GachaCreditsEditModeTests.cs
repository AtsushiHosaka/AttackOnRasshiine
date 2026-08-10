using System;
using AttackOnRasshiine.Runtime.Domain;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class GachaCreditsEditModeTests
    {
        [Test]
        public void DefaultRuleGrantsOneCreditPerApprovedHour()
        {
            var beforeHour = GachaCreditCalculator.CalculateFromApprovedMinutes(59);
            var oneHour = GachaCreditCalculator.CalculateFromApprovedMinutes(60);
            var almostThreeHours = GachaCreditCalculator.CalculateFromApprovedMinutes(179);

            Assert.AreEqual(0, beforeHour.TotalEarnedCredits);
            Assert.AreEqual(TimeSpan.FromMinutes(1), beforeHour.RemainingUntilNextCredit);
            Assert.AreEqual(1, oneHour.TotalEarnedCredits);
            Assert.AreEqual(2, almostThreeHours.TotalEarnedCredits);
            Assert.AreEqual(TimeSpan.FromMinutes(59), almostThreeHours.ProgressTowardNextCredit);
        }

        [Test]
        public void CreditIntervalCanBeConfigured()
        {
            var rules = new GachaCreditRules(TimeSpan.FromMinutes(30));

            var grant = GachaCreditCalculator.CalculateFromApprovedMinutes(90, rules: rules);

            Assert.AreEqual(3, grant.TotalEarnedCredits);
            Assert.AreEqual(3, grant.NewCredits);
            Assert.AreEqual(TimeSpan.Zero, grant.ProgressTowardNextCredit);
            Assert.AreEqual(TimeSpan.FromMinutes(30), grant.RemainingUntilNextCredit);
        }

        [Test]
        public void GrantedWatermarkMakesCreditCalculationIdempotent()
        {
            var firstRetry = GachaCreditCalculator.CalculateFromApprovedMinutes(185, previouslyGrantedCredits: 2);
            var secondRetry = GachaCreditCalculator.CalculateFromApprovedMinutes(185, previouslyGrantedCredits: 3);

            Assert.AreEqual(3, firstRetry.TotalEarnedCredits);
            Assert.AreEqual(1, firstRetry.NewCredits);
            Assert.AreEqual(0, secondRetry.NewCredits);
        }

        [Test]
        public void CreditIntervalMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GachaCreditRules(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GachaCreditRules(TimeSpan.FromMinutes(-1)));
        }
    }
}
