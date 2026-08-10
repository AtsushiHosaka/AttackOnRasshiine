using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Domain;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class DevelopmentSessionProgressionEditModeTests
    {
        [Test]
        public void ApprovedDevelopmentGrantsTimeScoreAndQualityAdjustedExp()
        {
            var progress = DevelopmentSessionProgression.CalculateApproved(90, new AiEvaluation
            {
                Rank = AiRank.A,
                TotalScore = 82
            });

            Assert.AreEqual(90, progress.ApprovedMinutes);
            Assert.AreEqual(90, progress.DevelopmentScoreGained);
            Assert.AreEqual(144, progress.ExpGained);
            Assert.AreEqual(82, progress.AiQualityScore);
        }

        [Test]
        public void ScoreRateIsConfigurableAndInvalidInputIsClamped()
        {
            var rules = new DevelopmentProgressionRules(scorePerApprovedMinute: 3);
            var progress = DevelopmentSessionProgression.CalculateApproved(-10, new AiEvaluation
            {
                Rank = AiRank.S,
                TotalScore = 125
            }, rules);

            Assert.AreEqual(0, progress.ApprovedMinutes);
            Assert.AreEqual(0, progress.DevelopmentScoreGained);
            Assert.AreEqual(0, progress.ExpGained);
            Assert.AreEqual(100, progress.AiQualityScore);
        }

        [Test]
        public void PendingAndRejectedSessionsDoNotGrantDurableProgress()
        {
            var session = new DevSession
            {
                Status = DevSessionStatus.Pending,
                DurationMinutes = 60,
                Evaluation = new AiEvaluation { Rank = AiRank.S, TotalScore = 98 }
            };

            Assert.AreEqual(0, DevelopmentSessionProgression.CalculateForSession(session).ExpGained);
            Assert.AreEqual(0, DevelopmentSessionProgression.CalculateForSession(session).DevelopmentScoreGained);

            session.Status = DevSessionStatus.Rejected;
            Assert.AreEqual(0, DevelopmentSessionProgression.CalculateForSession(session).ExpGained);
        }

        [Test]
        public void ApprovedSessionUsesSameExpRuleAsExistingPreview()
        {
            var session = new DevSession
            {
                Status = DevSessionStatus.Approved,
                DurationMinutes = 75,
                Evaluation = new AiEvaluation { Rank = AiRank.B, TotalScore = 65 }
            };

            var progress = DevelopmentSessionProgression.CalculateForSession(session);

            Assert.AreEqual(session.PreviewExp, progress.ExpGained);
            Assert.AreEqual(75, progress.DevelopmentScoreGained);
        }
    }
}
