using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleMpConsumptionEditModeTests
    {
        [Test]
        public void StrongAttackConsumesBaseMpAndRecordsCost()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            participant.CurrentMp = 12;

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Strong);

            Assert.AreEqual(BattleActionType.Strong, result.ActionType);
            Assert.AreEqual(10, result.MpCost);
            Assert.AreEqual(2, participant.CurrentMp);
            Assert.Greater(result.Damage, 0);
            Assert.AreEqual(result.Damage, participant.TotalDamage);
        }

        [Test]
        public void RifleFullPowerConsumesDiscountedMpCost()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[1];
            participant.CurrentMp = 17;

            var option = repository.GetBattleActionOptions(participant.UserId, WeaponKind.Rifle)
                .First(item => item.ActionType == BattleActionType.FullPower);
            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Rifle, BattleActionType.FullPower);

            Assert.AreEqual(17, option.MpCost);
            Assert.IsTrue(option.IsAvailable);
            Assert.AreEqual(BattleActionType.FullPower, result.ActionType);
            Assert.AreEqual(17, result.MpCost);
            Assert.AreEqual(0, participant.CurrentMp);
        }

        [Test]
        public void InsufficientMpFallsBackToNormalWithoutSpendingMp()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[2];
            participant.CurrentMp = 9;

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Blade, BattleActionType.Support);

            Assert.AreEqual(BattleActionType.Normal, result.ActionType);
            Assert.AreEqual(0, result.MpCost);
            Assert.AreEqual(9, participant.CurrentMp);
            Assert.AreEqual(0, participant.SupportCount);
            Assert.IsTrue(string.IsNullOrEmpty(result.SupportEffect));
            StringAssert.Contains("通常攻撃", result.Message);
        }
    }
}
