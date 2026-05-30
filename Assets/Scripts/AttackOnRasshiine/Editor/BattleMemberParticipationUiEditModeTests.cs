using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleMemberParticipationUiEditModeTests
    {
        [Test]
        public void PartyStatusAggregatesActiveParticipantHpAndMp()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var first = repository.ActiveBattle.Participants[0];
            var second = repository.ActiveBattle.Participants[1];
            first.CurrentHp = 10;
            first.CurrentMp = 5;
            second.CurrentHp = 0;
            second.CurrentMp = 3;

            var status = repository.GetBattlePartyStatus();

            Assert.AreEqual(repository.ActiveBattle.Participants.Count, status.ParticipantCount);
            Assert.AreEqual(repository.ActiveBattle.Participants.Count - 1, status.AliveCount);
            Assert.AreEqual(repository.ActiveBattle.Participants.Sum(participant => participant.CurrentHp), status.CurrentHp);
            Assert.AreEqual(repository.ActiveBattle.Participants.Sum(participant => participant.Stats.Hp), status.MaxHp);
            Assert.AreEqual(repository.ActiveBattle.Participants.Sum(participant => participant.CurrentMp), status.CurrentMp);
            Assert.AreEqual(repository.ActiveBattle.Participants.Sum(participant => participant.Stats.Mp), status.MaxMp);
        }

        [Test]
        public void ActionOptionsExposeMpCostAndAvailabilityForMemberMenu()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var member = repository.Members[0];
            var participant = repository.ActiveBattle.Participants.First(item => item.UserId == member.Id);
            participant.CurrentMp = 9;

            var options = repository.GetBattleActionOptions(member.Id, WeaponKind.Blade).ToList();

            Assert.AreEqual(5, options.Count);
            Assert.IsTrue(options.First(item => item.ActionType == BattleActionType.Normal).IsAvailable);
            Assert.IsFalse(options.First(item => item.ActionType == BattleActionType.Strong).IsAvailable);
            Assert.IsFalse(options.First(item => item.ActionType == BattleActionType.FullPower).IsAvailable);
            Assert.IsFalse(options.First(item => item.ActionType == BattleActionType.Support).IsAvailable);
            Assert.IsTrue(options.First(item => item.ActionType == BattleActionType.Guard).IsAvailable);
            StringAssert.Contains("MP10", options.First(item => item.ActionType == BattleActionType.Strong).Label);
        }

        [Test]
        public void MissingRequestedWeaponUsesBladeFallbackForActions()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var member = repository.Members[0];
            var missingWeapon = (WeaponKind)999;

            var options = repository.GetBattleActionOptions(member.Id, missingWeapon).ToList();
            var result = repository.SubmitBattleAction(member.Id, BattleRole.Attacker, missingWeapon, BattleActionType.Normal);

            Assert.AreEqual(5, options.Count);
            StringAssert.Contains("MP10", options.First(item => item.ActionType == BattleActionType.Strong).Label);
            Assert.AreEqual(WeaponKind.Blade, result.Weapon);
        }
    }
}
