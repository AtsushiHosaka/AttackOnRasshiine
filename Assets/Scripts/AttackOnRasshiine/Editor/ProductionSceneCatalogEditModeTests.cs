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
        }
    }
}
