using System;

namespace AttackOnRasshiine.Runtime.Domain
{
    public sealed class GachaCreditRules
    {
        public static readonly TimeSpan DefaultCreditInterval = TimeSpan.FromHours(1);

        public GachaCreditRules(TimeSpan creditInterval)
        {
            if (creditInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(creditInterval), "Credit interval must be positive.");
            }

            CreditInterval = creditInterval;
        }

        public TimeSpan CreditInterval { get; }
    }

    public sealed class GachaCreditGrant
    {
        public GachaCreditGrant(int totalEarnedCredits, int newCredits, TimeSpan progressTowardNextCredit, TimeSpan remainingUntilNextCredit)
        {
            TotalEarnedCredits = totalEarnedCredits;
            NewCredits = newCredits;
            ProgressTowardNextCredit = progressTowardNextCredit;
            RemainingUntilNextCredit = remainingUntilNextCredit;
        }

        public int TotalEarnedCredits { get; }
        public int NewCredits { get; }
        public TimeSpan ProgressTowardNextCredit { get; }
        public TimeSpan RemainingUntilNextCredit { get; }
    }

    /// <summary>
    /// Converts cumulative approved development time into idempotent gacha-credit grants.
    /// The caller persists TotalEarnedCredits as the granted watermark.
    /// </summary>
    public static class GachaCreditCalculator
    {
        public static GachaCreditGrant Calculate(
            TimeSpan totalApprovedDevelopmentTime,
            int previouslyGrantedCredits = 0,
            GachaCreditRules rules = null)
        {
            rules ??= new GachaCreditRules(GachaCreditRules.DefaultCreditInterval);
            var approvedTicks = Math.Max(0L, totalApprovedDevelopmentTime.Ticks);
            var intervalTicks = rules.CreditInterval.Ticks;
            var earnedLong = approvedTicks / intervalTicks;
            var earned = earnedLong >= int.MaxValue ? int.MaxValue : (int)earnedLong;
            var granted = Math.Max(0, previouslyGrantedCredits);
            var newlyGranted = Math.Max(0, earned - granted);
            var progressTicks = approvedTicks % intervalTicks;
            var progress = TimeSpan.FromTicks(progressTicks);
            var remaining = TimeSpan.FromTicks(intervalTicks - progressTicks);
            return new GachaCreditGrant(earned, newlyGranted, progress, remaining);
        }

        public static GachaCreditGrant CalculateFromApprovedMinutes(
            int totalApprovedMinutes,
            int previouslyGrantedCredits = 0,
            GachaCreditRules rules = null)
        {
            return Calculate(TimeSpan.FromMinutes(Math.Max(0, totalApprovedMinutes)), previouslyGrantedCredits, rules);
        }
    }
}
