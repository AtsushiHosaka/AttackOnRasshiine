using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class ProductionSceneCatalogEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            RasshiineWebRouteIntent.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RasshiineWebRouteIntent.Clear();
        }

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

        [Test]
        public void ProductionSceneAccessKeepsMemberAndMentorWorkspacesSeparate()
        {
            Assert.IsTrue(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Member, RasshiineProductionScene.MemberHome));
            Assert.IsTrue(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Member, RasshiineProductionScene.DevLog));
            Assert.IsFalse(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Mentor, RasshiineProductionScene.MemberHome));
            Assert.IsFalse(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Mentor, RasshiineProductionScene.DevLog));
            Assert.IsTrue(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Mentor, RasshiineProductionScene.MentorDashboard));
            Assert.IsFalse(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Member, RasshiineProductionScene.MentorDashboard));
            Assert.IsTrue(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Member, RasshiineProductionScene.Battle));
            Assert.IsTrue(RasshiineSceneCatalog.CanRoleAccessScene(UserRole.Mentor, RasshiineProductionScene.Battle));
        }

        [Test]
        public void WebRouteIntentAcceptsOnlyPublicAllowListAndPersistsBattleUntilConsumed()
        {
            Assert.IsTrue(RasshiineWebRouteIntent.TrySet("battle"));
            Assert.IsTrue(RasshiineWebRouteIntent.TryPeek(out var pending));
            Assert.AreEqual(RasshiineProductionScene.Battle, pending);

            Assert.IsFalse(RasshiineWebRouteIntent.TrySet("mentor-dashboard"));
            Assert.IsFalse(RasshiineWebRouteIntent.TrySet("../../battle"));
            Assert.IsTrue(RasshiineWebRouteIntent.TryPeek(out pending));
            Assert.AreEqual(RasshiineProductionScene.Battle, pending);
            Assert.IsFalse(RasshiineWebRouteIntent.Consume(RasshiineProductionScene.FrontDisplay));
            Assert.IsTrue(RasshiineWebRouteIntent.Consume(RasshiineProductionScene.Battle));
            Assert.IsFalse(RasshiineWebRouteIntent.TryPeek(out _));

            Assert.IsTrue(RasshiineWebRouteIntent.TrySet("front-display"));
            Assert.IsTrue(RasshiineWebRouteIntent.TryPeek(out pending));
            Assert.AreEqual(RasshiineProductionScene.FrontDisplay, pending);
            Assert.IsTrue(RasshiineWebRouteIntent.TrySet("home"));
            Assert.IsFalse(RasshiineWebRouteIntent.TryPeek(out _));
        }

        [Test]
        public void SceneBootstrapExposesTheExactSafeWebGlBridgeMethod()
        {
            var bridge = typeof(RasshiineSceneBootstrap).GetMethod(nameof(RasshiineSceneBootstrap.ApplyWebRoute));

            Assert.IsNotNull(bridge);
            Assert.IsTrue(bridge.IsPublic);
            CollectionAssert.AreEqual(new[] { typeof(string) }, System.Array.ConvertAll(bridge.GetParameters(), parameter => parameter.ParameterType));
        }
    }
}
