using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Scene;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public sealed class NeonCityBackdropSceneEditModeTests
    {
        private static readonly string[] BurgerFreeScenePaths =
            new[] { RasshiineSceneBuilder.PrototypeScenePath, "Assets/BackRock-NeonCity/Scenes/Neon City.unity" }
                .Concat(RasshiineSceneBuilder.ProductionScenePaths)
                .ToArray();

        [Test]
        public void ScenesDoNotPlaceBurgerProps()
        {
            foreach (var scenePath in BurgerFreeScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var burgerNames = scene.GetRootGameObjects()
                    .SelectMany(EnumerateSceneObjects)
                    .Select(gameObject => gameObject.name)
                    .Where(name => name.IndexOf("burger", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                CollectionAssert.IsEmpty(burgerNames, $"{scenePath} should not place burger props.");
            }
        }

        [Test]
        public void LoginPresetUsesTheAuthoredWaypointEnvironmentAndSharedMesh()
        {
            var harness = CreateRuntimeBackdropHarness();

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Login);

            var environments = FindNamedChildren(harness.Host, "Waypoint Terrace Environment");
            Assert.That(environments, Has.Length.EqualTo(1));
            Assert.That(environments[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(environments[0].IsChildOf(harness.SharedEnvironmentRoot), Is.True);
            Assert.That(environments[0].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(harness.AuthoredMesh));
            Assert.That(FindNamedChildren(harness.LoginRoot.gameObject, "Fantasy Meadow Backdrop"), Is.Empty);
        }

        [Test]
        public void HomePresetShowsHeroesWhileLoginKeepsThemOutOfTheRenderedHierarchy()
        {
            var harness = CreateRuntimeBackdropHarness();

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Login);
            Assert.That(FindNamedChildren(harness.HomeRoot.gameObject, "Home Hero"), Is.Empty);

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Home);
            var homeHero = FindNamedChildren(harness.HomeRoot.gameObject, "Home Hero").Single();
            Assert.That(homeHero.gameObject.activeInHierarchy, Is.True);
            Assert.That(harness.SharedEnvironmentRoot.gameObject.activeInHierarchy, Is.True);

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Login);
            Assert.That(homeHero.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void SwitchingBetweenLoginAndHomeKeepsExactlyOneEnvironmentInstance()
        {
            var harness = CreateRuntimeBackdropHarness();

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Login);
            var loginEnvironment = FindNamedChildren(harness.Host, "Waypoint Terrace Environment").Single();

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Home);
            var homeEnvironments = FindNamedChildren(harness.Host, "Waypoint Terrace Environment");
            Assert.That(homeEnvironments, Has.Length.EqualTo(1));
            Assert.That(homeEnvironments[0], Is.SameAs(loginEnvironment));
            Assert.That(FindNamedChildren(harness.LoginRoot.gameObject, "Waypoint Terrace Environment"), Is.Empty);
            Assert.That(FindNamedChildren(harness.HomeRoot.gameObject, "Waypoint Terrace Environment"), Is.Empty);

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Battle);
            Assert.That(harness.SharedEnvironmentRoot == null, Is.True, "Battle must release the exploration environment, not merely hide it.");
            Assert.That(FindNamedChildren(harness.Host, "Waypoint Terrace Environment"), Is.Empty);
        }

        [Test]
        public void BattleBackdropDoesNotDuplicateTheControllerOwnedArenaGeometry()
        {
            var harness = CreateRuntimeBackdropHarness();

            harness.Backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Battle);

            Assert.That(FindNamedChildren(harness.BattleRoot.gameObject, "Fantasy Meadow Backdrop"), Is.Empty);
            Assert.That(harness.BattleRoot.GetComponentsInChildren<MeshRenderer>(true), Is.Empty,
                "Battle backdrop owns sky/fog/light only; RaidBattleController is the sole arena geometry owner.");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static IEnumerable<GameObject> EnumerateSceneObjects(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
            {
                foreach (var gameObject in EnumerateSceneObjects(child.gameObject))
                {
                    yield return gameObject;
                }
            }
        }

        private static BackdropHarness CreateRuntimeBackdropHarness()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";

            var host = new GameObject("Backdrop Test Host");
            var loginRoot = new GameObject("Login Root").transform;
            var homeRoot = new GameObject("Home Root").transform;
            var battleRoot = new GameObject("Battle Root").transform;
            loginRoot.SetParent(host.transform, false);
            homeRoot.SetParent(host.transform, false);
            battleRoot.SetParent(host.transform, false);

            var authoredEnvironment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            authoredEnvironment.name = "Authored Waypoint Source";
            authoredEnvironment.transform.position = new Vector3(200f, 200f, 200f);
            var authoredMesh = authoredEnvironment.GetComponent<MeshFilter>().sharedMesh;

            var homeCamera = new GameObject("CAM_WaypointTerrace_Main", typeof(Camera));
            homeCamera.transform.SetParent(authoredEnvironment.transform, false);
            homeCamera.transform.localPosition = new Vector3(1.5f, 1.2f, -2.7f);
            homeCamera.transform.localRotation = Quaternion.LookRotation(new Vector3(-1.5f, -0.8f, 2.7f));
            homeCamera.GetComponent<Camera>().fieldOfView = 44f;

            var loginCamera = new GameObject("CAM_LoginPanorama", typeof(Camera));
            loginCamera.transform.SetParent(authoredEnvironment.transform, false);
            loginCamera.transform.localPosition = new Vector3(0f, 1.6f, -3.5f);
            loginCamera.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -1.1f, 3.5f));
            loginCamera.GetComponent<Camera>().fieldOfView = 59f;

            var heroPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heroPrefab.name = "Tiny Hero Source";
            heroPrefab.transform.position = new Vector3(220f, 220f, 220f);

            var backdrop = host.AddComponent<NeonCityBackdrop>();
            SetPrivateField(backdrop, "loginRoot", loginRoot);
            SetPrivateField(backdrop, "homeRoot", homeRoot);
            SetPrivateField(backdrop, "battleRoot", battleRoot);
            SetPrivateField(backdrop, "waypointTerracePrefab", authoredEnvironment);
            SetPrivateField(backdrop, "homeHeroPrefab", heroPrefab);
            SetPrivateField(backdrop, "fantasyBackdropBuilt", false);

            var buildMethod = typeof(NeonCityBackdrop).GetMethod(
                "BuildFantasyBackdrop",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);
            Assert.That(buildMethod, Is.Not.Null);
            buildMethod.Invoke(backdrop, new object[] { true });

            var sharedRoot = host.transform.Find("Shared Waypoint Terrace Backdrop");
            Assert.That(sharedRoot, Is.Not.Null);
            return new BackdropHarness(
                host,
                backdrop,
                loginRoot,
                homeRoot,
                battleRoot,
                sharedRoot,
                authoredMesh);
        }

        private static Transform[] FindNamedChildren(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Where(candidate => string.Equals(candidate.name, name, StringComparison.Ordinal))
                .ToArray();
        }

        private static void SetPrivateField<T>(NeonCityBackdrop backdrop, string fieldName, T value)
        {
            var field = typeof(NeonCityBackdrop).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected NeonCityBackdrop.{fieldName} to exist.");
            field.SetValue(backdrop, value);
        }

        private sealed class BackdropHarness
        {
            public BackdropHarness(
                GameObject host,
                NeonCityBackdrop backdrop,
                Transform loginRoot,
                Transform homeRoot,
                Transform battleRoot,
                Transform sharedEnvironmentRoot,
                Mesh authoredMesh)
            {
                Host = host;
                Backdrop = backdrop;
                LoginRoot = loginRoot;
                HomeRoot = homeRoot;
                BattleRoot = battleRoot;
                SharedEnvironmentRoot = sharedEnvironmentRoot;
                AuthoredMesh = authoredMesh;
            }

            public GameObject Host { get; }
            public NeonCityBackdrop Backdrop { get; }
            public Transform LoginRoot { get; }
            public Transform HomeRoot { get; }
            public Transform BattleRoot { get; }
            public Transform SharedEnvironmentRoot { get; }
            public Mesh AuthoredMesh { get; }
        }
    }
}
