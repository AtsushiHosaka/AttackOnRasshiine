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

            Assert.AreEqual(BattleOutcome.Win, summary.Outcome);
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

            Assert.AreEqual(BattleOutcome.Lose, summary.Outcome);
            Assert.IsFalse(summary.IsVictory);
            Assert.AreEqual("TIME UP", summary.ResultTitle);
            Assert.AreEqual(150, summary.BossCurrentHp);
            Assert.AreEqual(240, summary.TeamDamage);
            Assert.AreEqual(participant.UserId, summary.Contributors[0].UserId);
            Assert.IsTrue(summary.Contributors[0].IsMvp);
            StringAssert.Contains("参加報酬", summary.RewardSummary);
        }

        [Test]
        public void BossHpZeroCompletionStoresWinOutcome()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var member = repository.Members[0];
            var participant = repository.ActiveBattle.Participants.First(item => item.UserId == member.Id);
            participant.Stats.Atk = 120;
            repository.ActiveBattle.Boss.Def = 0;
            repository.ActiveBattle.Boss.CurrentHp = 1;

            repository.SubmitBattleAction(member.Id, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal);

            Assert.AreEqual(BattleStatus.Completed, repository.ActiveBattle.Status);
            Assert.AreEqual(BattlePhase.Completed, repository.ActiveBattle.Phase);
            Assert.AreEqual(BattleOutcome.Win, repository.ActiveBattle.Result);
            Assert.IsTrue(repository.GetBattleResultSummary().IsVictory);
        }

        [Test]
        public void TurnLimitCompletionStoresLoseOutcome()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var member = repository.Members[0];
            repository.ActiveBattle.TurnCount = 1;
            repository.ActiveBattle.Boss.CurrentHp = 999999;

            repository.SubmitBattleAction(member.Id, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);

            var summary = repository.GetBattleResultSummary();
            Assert.AreEqual(BattleStatus.Completed, repository.ActiveBattle.Status);
            Assert.AreEqual(BattlePhase.Completed, repository.ActiveBattle.Phase);
            Assert.AreEqual(BattleOutcome.Lose, repository.ActiveBattle.Result);
            Assert.AreEqual(BattleOutcome.Lose, summary.Outcome);
            Assert.IsFalse(summary.IsVictory);
        }

        [Test]
        public void SupabaseSnapshotCarriesSavedBattleOutcome()
        {
            var snapshot = new GameSnapshotDto
            {
                ActiveBattle = new BossBattleStateDto
                {
                    Id = "battle-test",
                    Boss = new MentorBossDto
                    {
                        Id = "boss-test",
                        Name = "メンター・テスト",
                        BossType = "コードマスター",
                        MaxHp = 100,
                        CurrentHp = 25
                    },
                    Status = (int)BattleStatus.Completed,
                    Phase = (int)BattlePhase.Completed,
                    Result = (int)BattleOutcome.Lose,
                    TurnNumber = 4,
                    TurnCount = 3
                }
            }.ToSnapshot();

            Assert.AreEqual(BattleOutcome.Lose, snapshot.ActiveBattle.Result);
            Assert.IsTrue(snapshot.ActiveBattle.IsCompleted);
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
