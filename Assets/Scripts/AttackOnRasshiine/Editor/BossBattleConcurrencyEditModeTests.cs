using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BossBattleConcurrencyEditModeTests
    {
        [Test]
        public void StartBattleKeepsExpectedMemberParticipantsReady()
        {
            var repository = new LocalGameRepository();

            repository.StartBattle();

            var battle = repository.ActiveBattle;
            Assert.AreEqual(BattleStatus.Active, battle.Status);
            Assert.AreEqual(repository.Members.Count, battle.Participants.Count);
            Assert.AreEqual(repository.Members.Count, battle.Participants.Select(participant => participant.UserId).Distinct().Count());
            Assert.IsTrue(battle.Participants.All(participant => participant.CurrentHp == participant.Stats.Hp));
            Assert.IsTrue(battle.Participants.All(participant => participant.CurrentMp == participant.Stats.Mp));
        }

        [Test]
        public void ExpectedMembersCanSubmitBattleActionsWithoutCorruptingState()
        {
            var repository = new LocalGameRepository();
            repository.SetBossHpMultiplier(100f);
            repository.StartBattle();
            repository.ActiveBattle.TurnCount = repository.Members.Count + 1;

            var results = SubmitWave(repository).ToList();

            Assert.AreEqual(repository.Members.Count, results.Count);
            Assert.AreEqual(repository.Members.Count, results.Select(result => result.UserId).Distinct().Count());
            Assert.AreEqual(repository.Members.Last().Id, repository.ActiveBattle.HighlightUserId);
            Assert.AreEqual(BattleStatus.Active, repository.ActiveBattle.Status);
            AssertBattleStateIsBounded(repository);
        }

        [Test]
        public void RepeatedMemberActionLoadKeepsBattleStable()
        {
            const int Rounds = 3;
            var repository = new LocalGameRepository();
            repository.SetBossHpMultiplier(100f);
            repository.StartBattle();
            repository.ActiveBattle.TurnCount = repository.Members.Count * Rounds + 1;
            var results = new List<BattleActionResult>();

            for (var round = 0; round < Rounds; round++)
            {
                results.AddRange(SubmitWave(repository));
                AssertBattleStateIsBounded(repository);
            }

            Assert.AreEqual(repository.Members.Count * Rounds, results.Count);
            Assert.IsTrue(results.All(result => !string.IsNullOrWhiteSpace(result.Message)));
            Assert.AreEqual(BattleStatus.Active, repository.ActiveBattle.Status);
            Assert.Greater(repository.ActiveBattle.TotalDamage, 0);
        }

        private static IEnumerable<BattleActionResult> SubmitWave(LocalGameRepository repository)
        {
            var roles = new[]
            {
                BattleRole.Attacker,
                BattleRole.Healer,
                BattleRole.Defender,
                BattleRole.Supporter,
                BattleRole.Attacker,
                BattleRole.Supporter
            };
            var weapons = new[]
            {
                WeaponKind.Blade,
                WeaponKind.Rifle,
                WeaponKind.Shield,
                WeaponKind.DebugTool,
                WeaponKind.Cannon,
                WeaponKind.ReleaseGear
            };
            var actions = new[]
            {
                BattleActionType.Normal,
                BattleActionType.Support,
                BattleActionType.Guard,
                BattleActionType.Strong,
                BattleActionType.FullPower,
                BattleActionType.Support
            };

            for (var index = 0; index < repository.Members.Count; index++)
            {
                var member = repository.Members[index];
                yield return repository.SubmitBattleAction(
                    member.Id,
                    roles[index % roles.Length],
                    weapons[index % weapons.Length],
                    actions[index % actions.Length]);
            }
        }

        private static void AssertBattleStateIsBounded(LocalGameRepository repository)
        {
            var battle = repository.ActiveBattle;
            Assert.AreEqual(repository.Members.Count, battle.Participants.Count);
            Assert.GreaterOrEqual(battle.Boss.CurrentHp, 0);
            Assert.LessOrEqual(battle.Boss.CurrentHp, battle.Boss.MaxHp);
            Assert.GreaterOrEqual(battle.TotalDamage, 0);

            foreach (var participant in battle.Participants)
            {
                Assert.GreaterOrEqual(participant.CurrentHp, 0);
                Assert.LessOrEqual(participant.CurrentHp, participant.Stats.Hp);
                Assert.GreaterOrEqual(participant.CurrentMp, 0);
                Assert.LessOrEqual(participant.CurrentMp, participant.Stats.Mp);
                Assert.GreaterOrEqual(participant.TotalDamage, 0);
                Assert.GreaterOrEqual(participant.TotalHeal, 0);
                Assert.GreaterOrEqual(participant.SupportCount, 0);
            }
        }
    }
}
