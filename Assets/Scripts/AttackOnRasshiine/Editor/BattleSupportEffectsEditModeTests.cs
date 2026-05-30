using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleSupportEffectsEditModeTests
    {
        [Test]
        public void HealerSupportRecordsActualTeamHpRestored()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var healer = repository.ActiveBattle.Participants[1];
            repository.ActiveBattle.Participants[0].CurrentHp -= 10;
            repository.ActiveBattle.Participants[1].CurrentHp -= 3;
            var beforeHp = repository.ActiveBattle.Participants.Sum(participant => participant.CurrentHp);

            var result = repository.SubmitBattleAction(healer.UserId, BattleRole.Healer, WeaponKind.Rifle, BattleActionType.Support);
            var restoredHp = repository.ActiveBattle.Participants.Sum(participant => participant.CurrentHp) - beforeHp;

            Assert.Greater(restoredHp, 0);
            Assert.AreEqual(restoredHp, result.Heal);
            Assert.AreEqual(restoredHp, healer.TotalHeal);
            Assert.IsTrue(repository.ActiveBattle.Participants.All(participant => participant.CurrentHp <= participant.Stats.Hp));
            StringAssert.Contains("チームHP", result.SupportEffect);
        }

        [Test]
        public void DefenderSupportRestoresTeamMpWithinCaps()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var defender = repository.ActiveBattle.Participants[2];
            foreach (var participant in repository.ActiveBattle.Participants)
            {
                participant.CurrentMp = Mathf.Max(0, participant.Stats.Mp - 4);
            }

            defender.CurrentMp = defender.Stats.Mp;
            var beforeMp = repository.ActiveBattle.Participants.Sum(participant => participant.CurrentMp);

            var result = repository.SubmitBattleAction(defender.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Support);
            var afterMp = repository.ActiveBattle.Participants.Sum(participant => participant.CurrentMp);

            Assert.AreEqual(10, result.MpCost);
            Assert.Greater(afterMp, beforeMp - result.MpCost);
            Assert.IsTrue(repository.ActiveBattle.Participants.All(participant => participant.CurrentMp <= participant.Stats.Mp));
            Assert.AreEqual(1, defender.SupportCount);
            StringAssert.Contains("MP", result.SupportEffect);
        }

        [Test]
        public void SupporterSupportReducesBossDefensePredictably()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var supporter = repository.ActiveBattle.Participants[3];
            repository.ActiveBattle.Boss.Def = 3;

            var result = repository.SubmitBattleAction(supporter.UserId, BattleRole.Supporter, WeaponKind.DebugTool, BattleActionType.Support);

            Assert.AreEqual(2, repository.ActiveBattle.Boss.Def);
            Assert.AreEqual(1, supporter.SupportCount);
            StringAssert.Contains("3->2", result.SupportEffect);
        }
    }
}
