using System.Linq;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class ProductionBattleSceneSeparationEditModeTests
    {
        [Test]
        public void ProductionCatalogSeparatesMemberBattleAndFrontDisplaySceneContracts()
        {
            var battle = ProductionSceneCatalog.Get(ProductionSceneKind.Battle);
            var front = ProductionSceneCatalog.Get(ProductionSceneKind.FrontDisplay);

            Assert.AreNotEqual(battle.ScenePath, front.ScenePath);
            Assert.IsTrue(battle.RequiresLogin);
            Assert.IsFalse(battle.IsReadOnly);
            Assert.IsFalse(front.RequiresLogin);
            Assert.IsTrue(front.IsReadOnly);
            Assert.Greater(front.PollingIntervalSeconds, 0f);
        }

        [Test]
        public void FrontDisplayPresentationIsReadOnlyAndCarriesBossState()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var summary = repository.GetFrontDisplaySummary();

            var presentation = BattleStatePresenter.ForFrontDisplay(summary);

            Assert.AreEqual(BattlePresentationMode.FrontDisplay, presentation.Mode);
            Assert.IsFalse(presentation.RequiresLogin);
            Assert.IsTrue(presentation.IsReadOnly);
            Assert.AreEqual(summary.BossName, presentation.BossName);
            Assert.AreEqual(summary.BossHpRatio, presentation.BossHpRatio);
            Assert.AreEqual(0, presentation.ActionLabels.Count);
        }

        [Test]
        public void BattlePresentationKeepsMemberActionOptionsSeparateFromFrontDisplay()
        {
            var repository = new LocalGameRepository();
            repository.StartBattle();
            var member = repository.Members[0];
            var participant = repository.ActiveBattle.Participants.First(item => item.UserId == member.Id);
            var options = repository.GetBattleActionOptions(member.Id, WeaponKind.Blade);

            var presentation = BattleStatePresenter.ForMember(repository.ActiveBattle, participant, options);

            Assert.AreEqual(BattlePresentationMode.Member, presentation.Mode);
            Assert.IsTrue(presentation.RequiresLogin);
            Assert.IsFalse(presentation.IsReadOnly);
            Assert.Greater(presentation.ActionLabels.Count, 0);
            StringAssert.Contains(participant.Nickname, presentation.PrimaryHighlight);
        }
    }
}
