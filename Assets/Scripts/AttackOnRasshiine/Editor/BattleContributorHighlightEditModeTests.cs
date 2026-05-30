using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleContributorHighlightEditModeTests
    {
        [Test]
        public void HighlightedContributorUsesContributionScoreAndContext()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            repository.StartBattle();
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);
            var damageLead = repository.ActiveBattle.Participants[0];
            var supportLead = repository.ActiveBattle.Participants[1];
            AddApprovedSession(repository, damageLead.UserId, nowUtc.AddMinutes(-20), 180);
            AddApprovedSession(repository, supportLead.UserId, nowUtc.AddMinutes(-10), 60);
            damageLead.TotalDamage = 500;
            supportLead.TotalDamage = 120;
            supportLead.TotalHeal = 260;
            supportLead.SupportCount = 5;

            var highlight = repository.GetHighlightedContributor(RankingPeriod.Weekly, nowUtc);
            var contributors = repository.GetBattleContributors(RankingPeriod.Weekly, nowUtc).ToList();

            Assert.AreEqual(supportLead.UserId, highlight.UserId);
            Assert.AreEqual(530, highlight.ContributionScore);
            Assert.IsTrue(highlight.IsMvp);
            Assert.AreEqual(highlight.UserId, contributors[0].UserId);
            StringAssert.Contains("攻撃 120", highlight.HighlightContext);
            StringAssert.Contains("回復 260", highlight.HighlightContext);
            StringAssert.Contains("支援 5回", highlight.HighlightContext);
        }

        private static void DisableSeedSessions(LocalGameRepository repository)
        {
            foreach (var session in repository.Sessions)
            {
                session.Status = DevSessionStatus.Rejected;
            }
        }

        private static void AddApprovedSession(LocalGameRepository repository, string userId, DateTime endedAtUtc, int durationMinutes)
        {
            var session = repository.StartSession(userId, "注目貢献者検証");
            session.StartedAtUtc = endedAtUtc.AddMinutes(-durationMinutes);
            session.EndedAtUtc = endedAtUtc;
            session.DurationMinutes = durationMinutes;
            session.Status = DevSessionStatus.Approved;
            session.Evaluation = new AiEvaluation
            {
                TotalScore = 80,
                Rank = AiRank.A,
                ExpMultiplier = 1.6f,
                Feedback = "注目貢献者検証",
                ModelName = "test"
            };
        }
    }
}
