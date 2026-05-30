using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleActionSelectionEditModeTests
    {
        [Test]
        public void SubmitBattleActionPersistsSelectionForUserAndTurn()
        {
            var repository = new LocalGameRepository();
            repository.SetBossHpMultiplier(100f);
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[1];
            var turnNumber = repository.ActiveBattle.TurnNumber;

            var result = repository.SubmitBattleAction(participant.UserId, BattleRole.Healer, WeaponKind.Rifle, BattleActionType.Support);
            var selection = repository.GetBattleActionSelection(participant.UserId, turnNumber);

            Assert.NotNull(selection);
            Assert.AreEqual(repository.ActiveBattle.Id, selection.BattleId);
            Assert.AreEqual(result.UserId, selection.UserId);
            Assert.AreEqual(result.Nickname, selection.Nickname);
            Assert.AreEqual(turnNumber, selection.TurnNumber);
            Assert.AreEqual(BattleRole.Healer, selection.Role);
            Assert.AreEqual(WeaponKind.Rifle, selection.Weapon);
            Assert.AreEqual(result.ActionType, selection.ActionType);
            Assert.AreEqual(result.MpCost, selection.MpCost);
            Assert.AreEqual(result.Damage, selection.Damage);
            Assert.AreEqual(result.Heal, selection.Heal);
            Assert.AreEqual(result.SupportEffect, selection.SupportEffect);
            Assert.AreEqual(selection, repository.ActiveBattle.ActionSelections.Single(item => item.UserId == participant.UserId && item.TurnNumber == turnNumber));
        }

        [Test]
        public void RepeatedSelectionForSameUserAndTurnReplacesPreviousChoice()
        {
            var repository = new LocalGameRepository();
            repository.SetBossHpMultiplier(100f);
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            var turnNumber = repository.ActiveBattle.TurnNumber;

            repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal);
            repository.ActiveBattle.TurnNumber = turnNumber;
            var updated = repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);

            var selections = repository.GetBattleActionSelections(turnNumber)
                .Where(selection => selection.UserId == participant.UserId)
                .ToList();
            Assert.AreEqual(1, selections.Count);
            Assert.AreEqual(BattleRole.Defender, selections[0].Role);
            Assert.AreEqual(WeaponKind.Shield, selections[0].Weapon);
            Assert.AreEqual(BattleActionType.Guard, selections[0].ActionType);
            Assert.AreEqual(updated.MpCost, selections[0].MpCost);
        }
    }
}
