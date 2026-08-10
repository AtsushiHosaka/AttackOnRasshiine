using System;
using System.IO;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.QA;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class WebGlVisualQaIntentEditModeTests
    {
        [TestCase("http://localhost:4189/?aor-visual-qa=1&screen=member-home")]
        [TestCase("https://127.0.0.1:9443/game?aor-visual-qa=1&screen=battle-active")]
        [TestCase("http://[::1]:8080/?screen=front-active&aor-visual-qa=1")]
        public void ParserAcceptsOnlyExplicitLoopbackRequests(string url)
        {
            Assert.IsTrue(WebGlVisualQaContext.TryParse(url, out var intent, out var reason), reason);
            Assert.IsNotNull(intent);
        }

        [TestCase("https://game.example.com/?aor-visual-qa=1&screen=member-home")]
        [TestCase("http://localhost.example.com/?aor-visual-qa=1&screen=member-home")]
        [TestCase("http://user@localhost:4189/?aor-visual-qa=1&screen=member-home")]
        [TestCase("file:///tmp/index.html?aor-visual-qa=1&screen=member-home")]
        [TestCase("http://localhost:4189/?aor-visual-qa=1&screen=member-home#unsafe")]
        [TestCase("http://localhost:4189/?aor-visual-qa=1&screen=member-home&role=mentor")]
        [TestCase("http://localhost:4189/?aor-visual-qa=1&aor-visual-qa=1&screen=member-home")]
        [TestCase("http://localhost:4189/?aor-visual-qa=1&screen=unknown")]
        [TestCase("http://localhost:4189/?aor-visual-qa=0&screen=member-home")]
        public void ParserRejectsRemoteAmbiguousOrUnlistedRequests(string url)
        {
            Assert.IsFalse(WebGlVisualQaContext.TryParse(url, out var intent, out var reason));
            Assert.IsNull(intent);
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void ParserRejectsOversizedUrl()
        {
            var url = "http://localhost:4189/?aor-visual-qa=1&screen=member-home&" +
                      new string('x', WebGlVisualQaContext.MaximumUrlLength);
            Assert.IsFalse(WebGlVisualQaContext.TryParse(url, out _, out var reason));
            Assert.AreEqual("url_length", reason);
        }

        [Test]
        public void EveryVisualQaEnumHasOneUniqueAllowListedSlug()
        {
            var slugs = WebGlVisualQaContext.AllowedScreenSlugs;
            Assert.AreEqual(Enum.GetValues(typeof(WebGlVisualQaScreen)).Length, slugs.Count);
            Assert.AreEqual(slugs.Count, slugs.Distinct(StringComparer.Ordinal).Count());

            var screens = slugs.Select(slug =>
            {
                Assert.IsTrue(WebGlVisualQaContext.TryResolveScreen(slug, out var intent), slug);
                Assert.AreEqual(slug, intent.Slug);
                return intent.Screen;
            }).ToArray();
            Assert.AreEqual(screens.Length, screens.Distinct().Count());
        }

        [Test]
        public void RoleAndSceneMappingCannotBeEscalatedByQueryData()
        {
            AssertMapping("member-products", RasshiineProductionScene.MemberHome, UserRole.Member);
            AssertMapping("mentor-products", RasshiineProductionScene.MentorDashboard, UserRole.Mentor);
            AssertMapping("battle-member-scheduled", RasshiineProductionScene.Battle, UserRole.Member);
            AssertMapping("battle-mentor-scheduled", RasshiineProductionScene.Battle, UserRole.Mentor);
            AssertMapping("front-active", RasshiineProductionScene.FrontDisplay, null);
            AssertMapping("login", RasshiineProductionScene.Login, null);
        }

        [Test]
        public void AuthoritativeCacheContainsNoFixtureIdentitySessionOrBattle()
        {
            var repository = LocalGameRepository.CreateAuthoritativeCache();

            Assert.IsEmpty(repository.Users);
            Assert.IsEmpty(repository.Sessions);
            Assert.IsEmpty(repository.Products);
            Assert.IsEmpty(repository.Achievements);
            Assert.IsEmpty(repository.AuditLogs);
            Assert.IsNull(repository.ActiveBattle);
            Assert.IsNotEmpty(repository.Weapons, "Static weapon definitions remain safe client-side catalog data.");
            Assert.IsNull(repository.Authenticate("member1", "password"));
        }

        [Test]
        public void VisualQaFixtureIsAvailableOnlyToGuardedEditorOrDevelopmentCompilation()
        {
            var repository = LocalGameRepository.CreateVisualQaFixture();

            Assert.IsNotEmpty(repository.Members);
            Assert.IsNotEmpty(repository.Mentors);
            Assert.IsNotEmpty(repository.Sessions);
            Assert.IsNotNull(repository.ActiveBattle);
        }

        [Test]
        public void ReleaseSourcesKeepQaAndCredentialFixturesBehindCompileGuards()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsNotNull(projectRoot);
            var qaSource = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/Scripts/AttackOnRasshiine/Runtime/QA/WebGlVisualQaIntent.cs"));
            var repositorySource = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/Scripts/AttackOnRasshiine/Runtime/Services/LocalGameRepository.cs"));
            var appSource = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/Scripts/AttackOnRasshiine/Runtime/UI/RaidGameApp.cs"));

            StringAssert.StartsWith("#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)", qaSource);
            StringAssert.Contains("LocalGameRepository.CreateAuthoritativeCache()", appSource);
            StringAssert.Contains("#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)\n        private void SeedUsers()", repositorySource);
            StringAssert.DoesNotContain("? \"password\" : value", repositorySource);
        }

        private static void AssertMapping(
            string slug,
            RasshiineProductionScene expectedScene,
            UserRole? expectedRole)
        {
            Assert.IsTrue(WebGlVisualQaContext.TryResolveScreen(slug, out var intent));
            Assert.AreEqual(expectedScene, intent.Scene);
            Assert.AreEqual(expectedRole, intent.Role);
        }
    }
}
