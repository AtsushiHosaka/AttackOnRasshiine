using AttackOnRasshiine.Runtime.Scene;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class ProductionSceneCatalogEditModeTests
    {
        [Test]
        public void ProductionSceneCatalogStartsFromBootAndRoutesToLogin()
        {
            var paths = RasshiineSceneCatalog.GetProductionScenePaths();

            Assert.AreEqual("Assets/Scenes/RasshiineBoot.unity", paths[0]);
            Assert.AreEqual("Assets/Scenes/RasshiineLogin.unity", paths[1]);
            Assert.AreEqual("RasshiineLogin", RasshiineSceneCatalog.GetSceneName(RasshiineProductionScene.Login));
            Assert.IsTrue(RasshiineSceneCatalog.IsProductionScenePath("Assets/Scenes/RasshiineBattle.unity"));
            Assert.IsFalse(RasshiineSceneCatalog.IsProductionScenePath(RasshiineSceneCatalog.PrototypeScenePath));
            Assert.IsTrue(RasshiineSceneCatalog.TryGetSceneByName("RasshiineBattle", out var matchedScene));
            Assert.AreEqual(RasshiineProductionScene.Battle, matchedScene);
            Assert.IsFalse(RasshiineSceneCatalog.TryGetSceneByName("Unknown", out var fallbackScene));
            Assert.AreEqual(RasshiineProductionScene.Login, fallbackScene);
        }

        [Test]
        public void FrontDisplayIsDisplayOnlyAndDoesNotRequireLogin()
        {
            Assert.IsTrue(RasshiineSceneCatalog.IsDisplayOnlyScene(RasshiineProductionScene.FrontDisplay));
            Assert.IsFalse(RasshiineSceneCatalog.RequiresAuthenticatedUser(RasshiineProductionScene.FrontDisplay));
            Assert.IsTrue(RasshiineSceneCatalog.RequiresAuthenticatedUser(RasshiineProductionScene.Battle));
            Assert.IsTrue(RasshiineSceneCatalog.RequiresAuthenticatedUser(RasshiineProductionScene.MentorDashboard));
        }
    }
}
