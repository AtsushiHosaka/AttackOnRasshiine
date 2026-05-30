using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleActionSelectionPersistenceEditModeTests
    {
        [Test]
        public void SubmittedActionPersistsForUserAndTurn()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Cannon, BattleActionType.Strong);
            var persisted = repository.ActiveBattle.Actions.Single();

            Assert.AreEqual(result.UserId, persisted.UserId);
            Assert.AreEqual(result.TurnNumber, persisted.TurnNumber);
            Assert.AreEqual(BattleRole.Attacker, persisted.Role);
            Assert.AreEqual(WeaponKind.Cannon, persisted.Weapon);
            Assert.AreEqual(BattleActionType.Strong, persisted.ActionType);
            Assert.AreEqual(result.MpCost, persisted.MpCost);
            Assert.AreEqual(result.Damage, persisted.Damage);
            Assert.AreEqual(result.Message, persisted.Message);
        }

        [Test]
        public void SameUserActionsPersistSeparatelyAcrossTurns()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[1];

            var first = repository.SubmitBattleAction(participant.UserId, BattleRole.Healer, WeaponKind.Rifle, BattleActionType.Support);
            var second = repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal);
            var userActions = repository.ActiveBattle.Actions.Where(action => action.UserId == participant.UserId).ToList();

            Assert.AreEqual(2, userActions.Count);
            Assert.AreEqual(first.TurnNumber, userActions[0].TurnNumber);
            Assert.AreEqual(second.TurnNumber, userActions[1].TurnNumber);
            Assert.AreNotEqual(userActions[0].TurnNumber, userActions[1].TurnNumber);
        }

        [Test]
        public void StartingBattleClearsPreviousActionSelections()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[2];
            repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);

            repository.StartBattle();

            Assert.AreEqual(0, repository.ActiveBattle.Actions.Count);
            Assert.AreEqual(1, repository.ActiveBattle.TurnNumber);
        }
    }
}
