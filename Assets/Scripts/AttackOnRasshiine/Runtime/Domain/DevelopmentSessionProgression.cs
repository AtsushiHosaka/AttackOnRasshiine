using System;
using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Domain
{
    /// <summary>
    /// Rules for the durable development score. EXP remains governed by
    /// DevelopmentExpCalculator so the existing rank multiplier table is preserved.
    /// </summary>
    public sealed class DevelopmentProgressionRules
    {
        public const int DefaultScorePerApprovedMinute = 1;

        public DevelopmentProgressionRules(int scorePerApprovedMinute = DefaultScorePerApprovedMinute)
        {
            if (scorePerApprovedMinute < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scorePerApprovedMinute));
            }

            ScorePerApprovedMinute = scorePerApprovedMinute;
        }

        public int ScorePerApprovedMinute { get; }
    }

    public sealed class DevelopmentSessionProgress
    {
        public DevelopmentSessionProgress(int approvedMinutes, int developmentScoreGained, int expGained, int aiQualityScore)
        {
            ApprovedMinutes = approvedMinutes;
            DevelopmentScoreGained = developmentScoreGained;
            ExpGained = expGained;
            AiQualityScore = aiQualityScore;
        }

        public int ApprovedMinutes { get; }
        public int DevelopmentScoreGained { get; }
        public int ExpGained { get; }
        public int AiQualityScore { get; }
    }

    /// <summary>
    /// Pure progression calculation. Development score rewards approved time, while
    /// EXP additionally reflects AI quality. Pending/rejected sessions never grant either.
    /// </summary>
    public static class DevelopmentSessionProgression
    {
        public static DevelopmentSessionProgress CalculateApproved(
            int approvedMinutes,
            AiEvaluation evaluation,
            DevelopmentProgressionRules rules = null)
        {
            rules ??= new DevelopmentProgressionRules();
            var minutes = Math.Max(0, approvedMinutes);
            var score = SaturatingMultiply(minutes, rules.ScorePerApprovedMinute);
            var exp = DevelopmentExpCalculator.Calculate(minutes, evaluation);
            var qualityScore = evaluation == null ? 0 : Clamp(evaluation.TotalScore, 0, 100);
            return new DevelopmentSessionProgress(minutes, score, exp, qualityScore);
        }

        public static DevelopmentSessionProgress CalculateForSession(
            DevSession session,
            DevelopmentProgressionRules rules = null)
        {
            if (session == null || session.Status != DevSessionStatus.Approved)
            {
                return new DevelopmentSessionProgress(0, 0, 0, 0);
            }

            return CalculateApproved(session.DurationMinutes, session.Evaluation, rules);
        }

        private static int SaturatingMultiply(int left, int right)
        {
            var result = (long)left * right;
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
