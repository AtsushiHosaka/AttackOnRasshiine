using UnityEngine;

namespace AttackOnRasshiine.Runtime.Data
{
    public static class DevelopmentExpCalculator
    {
        public const int ExpPerApprovedMinute = 1;

        public static int Calculate(int approvedMinutes, AiEvaluation evaluation)
        {
            return evaluation == null ? 0 : Calculate(approvedMinutes, evaluation.ExpMultiplier);
        }

        public static int Calculate(int approvedMinutes, float aiMultiplier)
        {
            var baseExp = Mathf.Max(0, approvedMinutes) * ExpPerApprovedMinute;
            return Mathf.RoundToInt(baseExp * Mathf.Max(0f, aiMultiplier));
        }
    }
}
