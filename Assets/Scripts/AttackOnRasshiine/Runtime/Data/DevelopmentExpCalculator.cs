using UnityEngine;

namespace AttackOnRasshiine.Runtime.Data
{
    public static class DevelopmentExpCalculator
    {
        public const int ExpPerApprovedMinute = 1;

        public static int Calculate(int approvedMinutes, AiEvaluation evaluation)
        {
            return evaluation == null ? 0 : Calculate(approvedMinutes, MultiplierForRank(evaluation.Rank));
        }

        public static int Calculate(int approvedMinutes, float aiMultiplier)
        {
            var baseExp = Mathf.Max(0, approvedMinutes) * ExpPerApprovedMinute;
            return Mathf.RoundToInt(baseExp * Mathf.Max(0f, aiMultiplier));
        }

        public static float MultiplierForRank(AiRank rank)
        {
            return rank switch
            {
                AiRank.S => 2.0f,
                AiRank.APlus => 1.8f,
                AiRank.A => 1.6f,
                AiRank.B => 1.3f,
                AiRank.C => 1.0f,
                _ => 0.8f
            };
        }
    }
}
