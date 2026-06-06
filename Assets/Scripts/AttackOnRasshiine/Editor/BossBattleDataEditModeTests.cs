using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class BossBattleDataEditModeTests
    {
        [Test]
        public void ScheduledBattleRecordsWeeklyHpStatusAndCreator()
        {
            var repository = new LocalGameRepository();
            var battle = repository.ActiveBattle;

            Assert.IsFalse(string.IsNullOrWhiteSpace(battle.Id));
            Assert.AreEqual(BattleStatus.Scheduled, battle.Status);
            Assert.AreEqual(BattleOutcome.Undecided, battle.Outcome);
            Assert.AreEqual(3, battle.TurnCount);
            Assert.Greater(battle.BaseHp, 0);
            Assert.AreEqual(1f, battle.HpMultiplier);
            Assert.AreEqual(battle.BaseHp, battle.Boss.MaxHp);
            Assert.AreEqual(battle.Boss.MaxHp, battle.Boss.CurrentHp);
            Assert.AreEqual(DayOfWeek.Monday, battle.WeekStartDateUtc.DayOfWeek);
            Assert.LessOrEqual((DateTime.UtcNow.Date - battle.WeekStartDateUtc.Date).TotalDays, 6);
            Assert.IsTrue(repository.Mentors.Any(mentor => mentor.Id == battle.CreatedByUserId));
            Assert.Greater(battle.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-1));
            Assert.IsNull(battle.StartedAtUtc);
            Assert.IsNull(battle.CompletedAtUtc);
        }

        [Test]
        public void HpMultiplierUsesWeeklyBaseHpWithoutCompounding()
        {
            var repository = new LocalGameRepository();
            var mentor = repository.Mentors[0];
            var baseHp = repository.ActiveBattle.BaseHp;

            repository.SetBossHpMultiplier(mentor.Id, 2f);
            Assert.AreEqual(2f, repository.ActiveBattle.HpMultiplier);
            Assert.AreEqual(Mathf.Max(2500, Mathf.RoundToInt(baseHp * 2f)), repository.ActiveBattle.Boss.MaxHp);
            Assert.AreEqual(repository.ActiveBattle.Boss.MaxHp, repository.ActiveBattle.Boss.CurrentHp);

            repository.SetBossHpMultiplier(mentor.Id, 3f);
            Assert.AreEqual(3f, repository.ActiveBattle.HpMultiplier);
            Assert.AreEqual(Mathf.Max(2500, Mathf.RoundToInt(baseHp * 3f)), repository.ActiveBattle.Boss.MaxHp);
        }

        [Test]
        public void BattleLifecycleRecordsStartCompletionAndResult()
        {
            var repository = new LocalGameRepository();
            var mentor = repository.Mentors[1];
            repository.ResetBattle(mentor.Id);

            Assert.AreEqual(mentor.Id, repository.ActiveBattle.CreatedByUserId);
            Assert.AreEqual(BattleStatus.Scheduled, repository.ActiveBattle.Status);

            repository.StartBattle(mentor.Id);
            var startedAtUtc = repository.ActiveBattle.StartedAtUtc;
            Assert.AreEqual(BattleStatus.Active, repository.ActiveBattle.Status);
            Assert.AreEqual(mentor.Id, repository.ActiveBattle.CreatedByUserId);
            Assert.IsNotNull(startedAtUtc);
            Assert.IsNull(repository.ActiveBattle.CompletedAtUtc);

            repository.ActiveBattle.TurnCount = 1;
            repository.ActiveBattle.Boss.CurrentHp = 999999;
            repository.ActiveBattle.Boss.Def = 9999;
            var participant = repository.ActiveBattle.Participants[0];

            repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);

            Assert.AreEqual(BattleStatus.Completed, repository.ActiveBattle.Status);
            Assert.AreEqual(BattleOutcome.Defeat, repository.ActiveBattle.Outcome);
            Assert.IsNotNull(repository.ActiveBattle.CompletedAtUtc);
            Assert.GreaterOrEqual(repository.ActiveBattle.CompletedAtUtc.Value, startedAtUtc.Value);
        }
    }
}
