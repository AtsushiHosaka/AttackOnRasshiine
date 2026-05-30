using System;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class FrontDisplaySummaryEditModeTests
    {
        [Test]
        public void ScheduledFrontDisplayShowsWaitingStateAndParticipantCapacity()
        {
            var repository = new LocalGameRepository();

            var summary = repository.GetFrontDisplaySummary();

            Assert.IsTrue(summary.IsScheduled);
            Assert.AreEqual("開始待機", summary.PhaseLabel);
            Assert.AreEqual(repository.Members.Count, summary.MemberCount);
            Assert.AreEqual(repository.ActiveBattle.Participants.Count, summary.ParticipantCount);
            Assert.AreEqual(1f, summary.BossHpRatio);
        }

        [Test]
        public void ActiveFrontDisplayOrdersHighlightsByContributionThenApprovedMinutes()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            repository.StartBattle();
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);
            var steady = repository.ActiveBattle.Participants[0];
            var featured = repository.ActiveBattle.Participants[1];
            AddApprovedSession(repository, steady.UserId, nowUtc.AddMinutes(-60), 180);
            AddApprovedSession(repository, featured.UserId, nowUtc.AddMinutes(-30), 90);
            steady.TotalDamage = 260;
            steady.TotalHeal = 10;
            featured.TotalDamage = 320;
            featured.TotalHeal = 50;
            featured.SupportCount = 2;
            repository.ActiveBattle.TotalDamage = steady.TotalDamage + featured.TotalDamage;

            var summary = repository.GetFrontDisplaySummary(RankingPeriod.Weekly, nowUtc);

            Assert.IsFalse(summary.IsScheduled);
            Assert.AreEqual("LIVE RAID", summary.PhaseLabel);
            Assert.AreEqual(featured.UserId, summary.TopHighlight.UserId);
            Assert.IsTrue(summary.TopHighlight.IsTopHighlight);
            Assert.AreEqual(90, summary.TopHighlight.ApprovedMinutes);
            Assert.AreEqual(580, summary.TeamDamage);
            Assert.Greater(summary.TopHighlight.ContributionScore, summary.Highlights[1].ContributionScore);
        }

        [Test]
        public void CompletedFrontDisplayCarriesResultSummary()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            repository.StartBattle();
            repository.ActiveBattle.Boss.CurrentHp = 0;
            repository.ActiveBattle.Status = BattleStatus.Completed;
            repository.ActiveBattle.Phase = BattlePhase.Completed;

            var summary = repository.GetFrontDisplaySummary();

            Assert.IsTrue(summary.IsCompleted);
            Assert.IsTrue(summary.IsVictory);
            Assert.AreEqual("RESULT", summary.PhaseLabel);
            Assert.AreEqual("VICTORY", summary.ResultTitle);
            StringAssert.Contains("勝利報酬", summary.RewardSummary);
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
            var session = repository.StartSession(userId, "前面表示検証");
            session.StartedAtUtc = endedAtUtc.AddMinutes(-durationMinutes);
            session.EndedAtUtc = endedAtUtc;
            session.DurationMinutes = durationMinutes;
            session.Status = DevSessionStatus.Approved;
            session.Evaluation = new AiEvaluation
            {
                TotalScore = 82,
                Rank = AiRank.A,
                ExpMultiplier = 1.6f,
                Feedback = "前面表示検証",
                ModelName = "test"
            };
        }
    }
}
