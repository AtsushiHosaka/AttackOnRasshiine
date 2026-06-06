using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class DevelopmentExpCalculationEditModeTests
    {
        [Test]
        public void CalculatorUsesApprovedMinutesAndAiMultiplier()
        {
            Assert.AreEqual(144, DevelopmentExpCalculator.Calculate(90, 1.6f));
            Assert.AreEqual(0, DevelopmentExpCalculator.Calculate(-30, 1.6f));
            Assert.AreEqual(0, DevelopmentExpCalculator.Calculate(90, -1f));
            Assert.AreEqual(0, DevelopmentExpCalculator.Calculate(90, (AiEvaluation)null));
        }

        [Test]
        public void CalculatorUsesSpecRankMultiplierTable()
        {
            AssertRankExp(AiRank.S, 200);
            AssertRankExp(AiRank.APlus, 180);
            AssertRankExp(AiRank.A, 160);
            AssertRankExp(AiRank.B, 130);
            AssertRankExp(AiRank.C, 100);
            AssertRankExp(AiRank.D, 80);
        }

        [Test]
        public void ApprovalAppliesCalculatedPreviewExpOnce()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            repository.StartSession(member.Id, "EXP計算式を確認する");
            var session = repository.CompleteSession(member.Id, 90, "仕様の式に沿って検証した", "承認後のEXP反映を見る");
            var stats = repository.GetStats(member.Id);
            var expBefore = stats.Exp;
            var expectedExp = DevelopmentExpCalculator.Calculate(session.DurationMinutes, session.Evaluation);

            repository.ApproveSession(session.Id, mentor.Id, "EXP計算を確認");

            Assert.AreEqual(expectedExp, session.PreviewExp);
            Assert.AreEqual(expBefore + expectedExp, stats.Exp);
        }

        private static void AssertRankExp(AiRank rank, int expectedExp)
        {
            var evaluation = new AiEvaluation
            {
                Rank = rank,
                ExpMultiplier = 0.01f
            };

            Assert.AreEqual(expectedExp, DevelopmentExpCalculator.Calculate(100, evaluation));
        }
    }
}
