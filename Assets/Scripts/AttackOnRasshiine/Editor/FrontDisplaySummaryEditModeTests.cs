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
            steady.TotalDamage = 320;
            steady.TotalHeal = 50;
            steady.SupportCount = 2;
            featured.TotalDamage = 320;
            featured.TotalHeal = 50;
            featured.SupportCount = 2;
            repository.ActiveBattle.TotalDamage = steady.TotalDamage + featured.TotalDamage;

            var summary = repository.GetFrontDisplaySummary(RankingPeriod.Weekly, nowUtc);

            Assert.IsFalse(summary.IsScheduled);
            Assert.AreEqual("LIVE RAID", summary.PhaseLabel);
            Assert.AreEqual(steady.UserId, summary.TopHighlight.UserId);
            Assert.IsTrue(summary.TopHighlight.IsTopHighlight);
            Assert.AreEqual(180, summary.TopHighlight.ApprovedMinutes);
            Assert.AreEqual(640, summary.TeamDamage);
            Assert.AreEqual(summary.TopHighlight.ContributionScore, summary.Highlights[1].ContributionScore);
            Assert.Greater(summary.TopHighlight.ApprovedMinutes, summary.Highlights[1].ApprovedMinutes);
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

        [Test]
        public void PublicFrontDisplaySnapshotUpdatesBattleWithoutClearingLocalUsers()
        {
            var repository = new LocalGameRepository();
            var memberCount = repository.Members.Count;
            var mentorCount = repository.Mentors.Count;
            var remoteBattle = new BossBattleState
            {
                Id = "remote-battle",
                CreatedAtUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc),
                Status = BattleStatus.Active,
                Phase = BattlePhase.ActionSelect,
                TurnNumber = 0,
                TurnCount = 0,
                Boss = new MentorBoss
                {
                    Id = "remote-boss",
                    Name = "Remote Boss",
                    MaxHp = 8000,
                    CurrentHp = 6400,
                    Def = 5
                }
            };

            remoteBattle.Participants.Add(new BattleParticipant
            {
                UserId = "remote-member",
                Nickname = "Remote Member",
                Stats = new CharacterStats { Level = 3, Hp = 120, Mp = 36 },
                Role = BattleRole.Attacker,
                Weapon = WeaponKind.Blade,
                CurrentHp = 120,
                CurrentMp = 20,
                TotalDamage = 450
            });

            repository.ApplyBattleSnapshot(remoteBattle);

            Assert.AreEqual(memberCount, repository.Members.Count);
            Assert.AreEqual(mentorCount, repository.Mentors.Count);
            Assert.AreEqual("remote-battle", repository.ActiveBattle.Id);
            Assert.AreEqual(1, repository.ActiveBattle.TurnNumber);
            Assert.AreEqual(1, repository.ActiveBattle.TurnCount);
            Assert.AreEqual(6400, repository.GetFrontDisplaySummary().BossCurrentHp);
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
