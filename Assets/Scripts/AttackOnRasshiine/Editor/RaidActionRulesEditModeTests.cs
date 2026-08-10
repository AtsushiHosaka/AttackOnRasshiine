using System;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Domain;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidActionRulesEditModeTests
    {
        [Test]
        public void ValidOwnedAffordableActionIsAccepted()
        {
            var result = RaidActionValidator.Validate(Context(CreateBattle()));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(RaidActionValidationFailure.None, result.Failure);
        }

        [Test]
        public void SameParticipantCannotSubmitTwiceForOneTurn()
        {
            var battle = CreateBattle();
            battle.Actions.Add(new BattleActionResult { UserId = "member-1", TurnNumber = 1 });

            var result = RaidActionValidator.Validate(Context(battle));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RaidActionValidationFailure.ActionAlreadySubmitted, result.Failure);
        }

        [TestCase(RaidActionValidationFailure.BattleNotActive)]
        [TestCase(RaidActionValidationFailure.BattleCompleted)]
        [TestCase(RaidActionValidationFailure.ParticipantNotFound)]
        [TestCase(RaidActionValidationFailure.ParticipantDefeated)]
        [TestCase(RaidActionValidationFailure.WeaponLocked)]
        [TestCase(RaidActionValidationFailure.InsufficientMp)]
        public void InvalidRaidStatesReturnSpecificFailure(RaidActionValidationFailure expectedFailure)
        {
            var battle = CreateBattle();
            var context = Context(battle);
            switch (expectedFailure)
            {
                case RaidActionValidationFailure.BattleNotActive:
                    battle.Status = BattleStatus.Scheduled;
                    break;
                case RaidActionValidationFailure.BattleCompleted:
                    battle.Status = BattleStatus.Completed;
                    break;
                case RaidActionValidationFailure.ParticipantNotFound:
                    context.UserId = "missing";
                    break;
                case RaidActionValidationFailure.ParticipantDefeated:
                    battle.Participants[0].CurrentHp = 0;
                    break;
                case RaidActionValidationFailure.WeaponLocked:
                    context.Weapon = WeaponKind.Cannon;
                    break;
                case RaidActionValidationFailure.InsufficientMp:
                    context.MpCost = battle.Participants[0].CurrentMp + 1;
                    break;
            }

            var result = RaidActionValidator.Validate(context);

            Assert.AreEqual(expectedFailure, result.Failure);
        }

        [Test]
        public void LocalRepositoryRejectsDuplicateBeforeMutatingBattleState()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            repository.ActiveBattle.TurnCount = 10;
            var participant = repository.ActiveBattle.Participants[0];
            var first = repository.SubmitBattleAction(
                participant.UserId,
                BattleRole.Attacker,
                WeaponKind.Blade,
                BattleActionType.Normal);
            repository.ActiveBattle.TurnNumber = first.TurnNumber;
            var bossHp = repository.ActiveBattle.Boss.CurrentHp;
            var mp = participant.CurrentMp;
            var role = participant.Role;
            var weapon = participant.Weapon;

            Assert.Throws<InvalidOperationException>(() => repository.SubmitBattleAction(
                participant.UserId,
                BattleRole.Healer,
                WeaponKind.Blade,
                BattleActionType.Support));

            Assert.AreEqual(bossHp, repository.ActiveBattle.Boss.CurrentHp);
            Assert.AreEqual(mp, participant.CurrentMp);
            Assert.AreEqual(role, participant.Role);
            Assert.AreEqual(weapon, participant.Weapon);
            Assert.AreEqual(1, repository.ActiveBattle.Actions.Count);
        }

        private static RaidActionValidationContext Context(BossBattleState battle)
        {
            return new RaidActionValidationContext
            {
                Battle = battle,
                UserId = "member-1",
                Role = BattleRole.Attacker,
                Weapon = WeaponKind.Blade,
                ActionType = BattleActionType.Strong,
                MpCost = 10
            };
        }

        private static BossBattleState CreateBattle()
        {
            var stats = new CharacterStats
            {
                Hp = 100,
                Mp = 30,
                UnlockedWeapons = new List<WeaponKind> { WeaponKind.Blade }
            };
            return new BossBattleState
            {
                Boss = new MentorBoss { MaxHp = 1000, CurrentHp = 1000 },
                Status = BattleStatus.Active,
                Phase = BattlePhase.ActionSelect,
                Outcome = BattleOutcome.Undecided,
                TurnNumber = 1,
                TurnCount = 3,
                Participants = new List<BattleParticipant>
                {
                    new()
                    {
                        UserId = "member-1",
                        Stats = stats,
                        CurrentHp = stats.Hp,
                        CurrentMp = stats.Mp,
                        Weapon = WeaponKind.Blade
                    }
                },
                Actions = new List<BattleActionResult>()
            };
        }
    }
}
