using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleDamageCalculationEditModeTests
    {
        [Test]
        public void CalculatorUsesStatsRoleActionWeaponAndDefense()
        {
            var stats = new CharacterStats { Atk = 32 };
            var weapon = new WeaponDefinition { DamageMultiplier = 1.25f };
            var boss = new MentorBoss { Def = 7 };

            var damage = BattleDamageCalculator.Calculate(stats, weapon, boss, BattleRole.Attacker, BattleActionType.FullPower);

            Assert.AreEqual(143, damage);
        }

        [Test]
        public void CalculatorClampsNegativeDamageToZero()
        {
            var damage = BattleDamageCalculator.Calculate(10, 1.0f, 999, BattleRole.Defender, BattleActionType.Guard);

            Assert.AreEqual(0, damage);
        }

        [Test]
        public void SubmitBattleActionPersistsCalculatedDamageAndShowsMessage()
        {
            var repository = new LocalGameRepository();
            repository.SetBossHpMultiplier(100f);
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            participant.Stats.Atk = 32;
            participant.CurrentMp = 30;
            repository.ActiveBattle.Boss.Def = 7;
            foreach (var other in repository.ActiveBattle.Participants.Where(item => item.UserId != participant.UserId))
            {
                other.CurrentHp = 0;
            }

            var weapon = repository.Weapons.First(item => item.Kind == WeaponKind.Cannon);
            var expectedDamage = BattleDamageCalculator.Calculate(participant.Stats, weapon, repository.ActiveBattle.Boss, BattleRole.Attacker, BattleActionType.FullPower);
            var bossHpBefore = repository.ActiveBattle.Boss.CurrentHp;

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Cannon, BattleActionType.FullPower);

            Assert.AreEqual(expectedDamage, result.Damage);
            Assert.AreEqual(expectedDamage, participant.TotalDamage);
            Assert.AreEqual(expectedDamage, repository.ActiveBattle.TotalDamage);
            Assert.AreEqual(bossHpBefore - expectedDamage, repository.ActiveBattle.Boss.CurrentHp);
            StringAssert.Contains($"{expectedDamage}ダメージ", result.Message);
        }
    }
}
