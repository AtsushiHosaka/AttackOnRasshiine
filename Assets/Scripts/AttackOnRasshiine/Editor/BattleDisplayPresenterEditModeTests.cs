using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleDisplayPresenterEditModeTests
    {
        [Test]
        public void BattlePresenterBuildsMemberActionStateFromSharedBattleState()
        {
            var repository = new LocalGameRepository();
            var presenter = new BattleDisplayPresenter();
            repository.StartBattle();
            var member = repository.Members.First(user => repository.GetParticipant(user.Id) != null);

            var state = presenter.BuildBattle(repository, member, WeaponKind.Blade, true, true);

            Assert.AreSame(repository.ActiveBattle, state.Battle);
            Assert.IsTrue(state.CanSubmitAction);
            Assert.IsFalse(state.CanStartBattle);
            Assert.AreEqual("API同期", state.SyncModeLabel);
            Assert.IsTrue(state.IsSyncing);
            Assert.Greater(state.ActionOptions.Count, 0);
        }

        [Test]
        public void BattlePresenterBuildsMentorStartState()
        {
            var repository = new LocalGameRepository();
            var presenter = new BattleDisplayPresenter();
            var mentor = repository.Mentors[0];

            var state = presenter.BuildBattle(repository, mentor, WeaponKind.Blade, false, false);

            Assert.IsTrue(state.CanStartBattle);
            Assert.IsFalse(state.CanSubmitAction);
            Assert.AreEqual("デモ同期", state.SyncModeLabel);
            Assert.AreEqual(BattleStatus.Scheduled, state.Battle.Status);
        }

        [Test]
        public void FrontDisplayPresenterSupportsDisplayOnlyPollingState()
        {
            var repository = new LocalGameRepository();
            var presenter = new BattleDisplayPresenter();
            repository.StartBattle();
            var participant = repository.ActiveBattle.Participants[0];
            repository.SubmitBattleAction(participant.UserId, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal);

            var state = presenter.BuildFrontDisplay(repository, true, true, true);

            Assert.IsTrue(state.IsDisplayOnly);
            Assert.IsTrue(state.IsPolling);
            Assert.AreEqual("API表示同期", state.SyncModeLabel);
            Assert.AreEqual(repository.ActiveBattle.Actions.Count, state.LastUpdatedActionCount);
            Assert.AreEqual("LIVE RAID", state.Summary.PhaseLabel);
            Assert.IsNotNull(state.Summary.TopHighlight);
        }
    }
}
