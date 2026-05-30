using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleTurnFlowEditModeTests
    {
        [Test]
        public void BattlePhaseProgressesWithoutRealtimeDependency()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();

            Assert.AreEqual(BattlePhase.TurnStart, repository.ActiveBattle.Phase);

            repository.AdvanceBattlePhase();
            Assert.AreEqual(BattlePhase.ActionSelect, repository.ActiveBattle.Phase);

            repository.AdvanceBattlePhase();
            Assert.AreEqual(BattlePhase.Resolving, repository.ActiveBattle.Phase);

            repository.AdvanceBattlePhase();
            Assert.AreEqual(BattlePhase.Result, repository.ActiveBattle.Phase);

            repository.AdvanceBattlePhase();
            Assert.AreEqual(2, repository.ActiveBattle.TurnNumber);
            Assert.AreEqual(BattlePhase.TurnStart, repository.ActiveBattle.Phase);
        }

        [Test]
        public void ThreeSubmittedTurnsCompleteBattleWithoutRealtimeTimers()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            repository.ActiveBattle.TurnCount = 3;
            repository.ActiveBattle.Boss.CurrentHp = 999999;
            repository.ActiveBattle.Boss.Def = 9999;
            var participant = repository.ActiveBattle.Participants[0];

            repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);
            Assert.AreEqual(BattleStatus.Active, repository.ActiveBattle.Status);
            Assert.AreEqual(2, repository.ActiveBattle.TurnNumber);
            Assert.AreEqual(BattlePhase.TurnStart, repository.ActiveBattle.Phase);

            repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);
            Assert.AreEqual(BattleStatus.Active, repository.ActiveBattle.Status);
            Assert.AreEqual(3, repository.ActiveBattle.TurnNumber);
            Assert.AreEqual(BattlePhase.TurnStart, repository.ActiveBattle.Phase);

            repository.SubmitBattleAction(participant.UserId, BattleRole.Defender, WeaponKind.Shield, BattleActionType.Guard);
            Assert.AreEqual(BattleStatus.Completed, repository.ActiveBattle.Status);
            Assert.AreEqual(BattlePhase.Completed, repository.ActiveBattle.Phase);
            Assert.AreEqual(BattleOutcome.Defeat, repository.ActiveBattle.Outcome);
        }
    }
}
