using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleResultSummaryEditModeTests
    {
        [Test]
        public void BattleResultSummaryMarksVictoryAndMvpReward()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            repository.StartBattle();
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);
            var first = repository.ActiveBattle.Participants[0];
            var mvp = repository.ActiveBattle.Participants[1];
            AddApprovedSession(repository, first.UserId, nowUtc.AddMinutes(-20), 60);
            AddApprovedSession(repository, mvp.UserId, nowUtc.AddMinutes(-10), 120);
            first.TotalDamage = 320;
            first.TotalHeal = 40;
            first.SupportCount = 1;
            mvp.TotalDamage = 680;
            mvp.TotalHeal = 10;
            repository.ActiveBattle.TotalDamage = first.TotalDamage + mvp.TotalDamage;
            repository.ActiveBattle.Boss.CurrentHp = 0;
            repository.ActiveBattle.Status = BattleStatus.Completed;
            repository.ActiveBattle.Phase = BattlePhase.Completed;

            var summary = repository.GetBattleResultSummary(RankingPeriod.Weekly, nowUtc);

            Assert.AreEqual(BattleOutcome.Victory, repository.ActiveBattle.Outcome);
            Assert.AreEqual(BattleOutcome.Victory, summary.Outcome);
            Assert.IsTrue(summary.IsVictory);
            Assert.AreEqual("VICTORY", summary.ResultTitle);
            Assert.AreEqual(0, summary.BossCurrentHp);
            Assert.AreEqual(1000, summary.TeamDamage);
            Assert.AreEqual(mvp.UserId, summary.Contributors[0].UserId);
            Assert.IsTrue(summary.Contributors[0].IsMvp);
            Assert.Greater(summary.Contributors[0].RewardExp, summary.Contributors[1].RewardExp);
            StringAssert.Contains(mvp.Nickname, summary.RewardSummary);
        }

        [Test]
        public void BattleResultSummaryMarksTimeUpWithRemainingBossHp()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            participant.TotalDamage = 240;
            participant.SupportCount = 2;
            repository.ActiveBattle.TotalDamage = participant.TotalDamage;
            repository.ActiveBattle.Boss.CurrentHp = 150;
            repository.ActiveBattle.Status = BattleStatus.Completed;
            repository.ActiveBattle.Phase = BattlePhase.Completed;

            var summary = repository.GetBattleResultSummary();

            Assert.AreEqual(BattleOutcome.Defeat, repository.ActiveBattle.Outcome);
            Assert.AreEqual(BattleOutcome.Defeat, summary.Outcome);
            Assert.IsFalse(summary.IsVictory);
            Assert.AreEqual("TIME UP", summary.ResultTitle);
            Assert.AreEqual(150, summary.BossCurrentHp);
            Assert.AreEqual(240, summary.TeamDamage);
            Assert.AreEqual(participant.UserId, summary.Contributors[0].UserId);
            Assert.IsTrue(summary.Contributors[0].IsMvp);
            StringAssert.Contains("参加報酬", summary.RewardSummary);
        }

        [Test]
        public void BattleActionSavesVictoryOutcomeWhenBossHpReachesZero()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            repository.ActiveBattle.Boss.CurrentHp = 1;
            repository.ActiveBattle.Boss.Def = 0;

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal);
            var summary = repository.GetBattleResultSummary();

            Assert.Greater(result.Damage, 0);
            Assert.AreEqual(BattleStatus.Completed, repository.ActiveBattle.Status);
            Assert.AreEqual(BattlePhase.Completed, repository.ActiveBattle.Phase);
            Assert.AreEqual(BattleOutcome.Victory, repository.ActiveBattle.Outcome);
            Assert.AreEqual(BattleOutcome.Victory, summary.Outcome);
            Assert.IsTrue(summary.IsVictory);
            Assert.AreEqual("VICTORY", summary.ResultTitle);
        }

        [Test]
        public void BattleActionSavesDefeatOutcomeWhenTurnLimitEnds()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            repository.ActiveBattle.TurnCount = 1;
            repository.ActiveBattle.Boss.CurrentHp = 999999;
            repository.ActiveBattle.Boss.Def = 9999;

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);
            var summary = repository.GetBattleResultSummary();

            Assert.AreEqual(0, result.Damage);
            Assert.Greater(repository.ActiveBattle.Boss.CurrentHp, 0);
            Assert.AreEqual(BattleStatus.Completed, repository.ActiveBattle.Status);
            Assert.AreEqual(BattlePhase.Completed, repository.ActiveBattle.Phase);
            Assert.AreEqual(BattleOutcome.Defeat, repository.ActiveBattle.Outcome);
            Assert.AreEqual(BattleOutcome.Defeat, summary.Outcome);
            Assert.IsFalse(summary.IsVictory);
            Assert.AreEqual("TIME UP", summary.ResultTitle);
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
            var session = repository.StartSession(userId, "リザルト検証");
            session.StartedAtUtc = endedAtUtc.AddMinutes(-durationMinutes);
            session.EndedAtUtc = endedAtUtc;
            session.DurationMinutes = durationMinutes;
            session.Status = DevSessionStatus.Approved;
            session.Evaluation = new AiEvaluation
            {
                TotalScore = 80,
                Rank = AiRank.A,
                ExpMultiplier = 1.6f,
                Feedback = "リザルト検証",
                ModelName = "test"
            };
        }
    }
}
