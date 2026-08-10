using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public sealed class WaypointTerraceAssetEditModeTests
    {
        private const string EnvironmentPath = "Assets/Art/WaypointTerrace/WaypointTerrace_FullEnvironment.fbx";

        [Test]
        public void FullEnvironment_IsImportedAndWithinWebGlTriangleBudget()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
            Assert.That(prefab, Is.Not.Null, $"Missing Blender export at {EnvironmentPath}");

            var meshes = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Distinct()
                .ToArray();
            var triangles = meshes.Sum(mesh => mesh.triangles.Length / 3);

            Assert.That(meshes.Length, Is.GreaterThan(0));
            Assert.That(triangles, Is.GreaterThan(1000));
            Assert.That(triangles, Is.LessThanOrEqualTo(8000), "Home environment must remain lightweight for WebGL/mobile.");
        }

        [Test]
        public void HomeBackdrop_UsesAuthoredCameraAndKeepsHeroWaypointInFrame()
        {
            var environmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            Assert.That(environmentPrefab, Is.Not.Null);
            Assert.That(heroPrefab, Is.Not.Null);
            Assert.That(environmentPrefab.GetComponentsInChildren<Transform>(true)
                .Any(transform => transform.name.Contains("CAM_WaypointTerrace")), Is.True, "Blender camera anchor must be included in the FBX import.");

            var mainCameraObject = new GameObject("Waypoint Test Main Camera", typeof(Camera), typeof(RaidFollowCamera));
            mainCameraObject.tag = "MainCamera";
            mainCameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2.75f, -10.8f), Quaternion.Euler(13f, 0f, 0f));
            var mainCamera = mainCameraObject.GetComponent<Camera>();
            mainCamera.fieldOfView = 48f;
            var defaultPosition = mainCamera.transform.position;

            var host = new GameObject("Waypoint Backdrop Test Host", typeof(NeonCityBackdrop));
            var loginRoot = new GameObject("Login Street Set").transform;
            var homeRoot = new GameObject("Home Base Set").transform;
            var battleRoot = new GameObject("Battle Backdrop Set").transform;
            loginRoot.SetParent(host.transform, false);
            homeRoot.SetParent(host.transform, false);
            battleRoot.SetParent(host.transform, false);
            var backdrop = host.GetComponent<NeonCityBackdrop>();
            var serialized = new SerializedObject(backdrop);
            serialized.FindProperty("loginRoot").objectReferenceValue = loginRoot;
            serialized.FindProperty("homeRoot").objectReferenceValue = homeRoot;
            serialized.FindProperty("battleRoot").objectReferenceValue = battleRoot;
            serialized.FindProperty("waypointTerracePrefab").objectReferenceValue = environmentPrefab;
            serialized.FindProperty("homeHeroPrefab").objectReferenceValue = heroPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                var buildMethod = typeof(NeonCityBackdrop).GetMethod(
                    "BuildFantasyBackdrop",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
                Assert.That(buildMethod, Is.Not.Null);
                buildMethod.Invoke(backdrop, new object[] { true });

                backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Home);
                var environment = host.transform.Find("Shared Waypoint Terrace Backdrop/Waypoint Terrace Environment");
                Assert.That(environment, Is.Not.Null);

                var authoredCameraAnchor = environment.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name.Contains("CAM_WaypointTerrace"));
                var authoredCamera = authoredCameraAnchor.GetComponent<Camera>();
                if (authoredCamera != null)
                {
                    Assert.That(authoredCamera.enabled, Is.False);
                }
                Assert.That(Vector3.Distance(mainCamera.transform.position, authoredCameraAnchor.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(mainCamera.transform.rotation, authoredCameraAnchor.rotation), Is.LessThan(0.01f));
                Assert.That(mainCameraObject.GetComponent<RaidFollowCamera>().enabled, Is.False);

                var hero = homeRoot.Find("Home Hero");
                Assert.That(hero, Is.Not.Null);
                var heroBounds = RendererBounds(hero.gameObject);
                var waypointBounds = NamedRendererBounds(environment.gameObject, "WPT_Crystal_Main");
                AssertInViewport(mainCamera, heroBounds.center, "Home Hero");
                AssertInViewport(mainCamera, waypointBounds.center, "Waypoint crystal");
                var heroViewport = mainCamera.WorldToViewportPoint(heroBounds.center);
                Assert.That(heroViewport.x, Is.InRange(0.08f, 0.40f), "Home Hero must hold the reference's lower-left visual anchor.");
                Assert.That(heroViewport.y, Is.InRange(0.30f, 0.68f), "Home Hero must remain vertically balanced above the primary CTA.");

                var stageBounds = NamedRendererBounds(environment.gameObject, "REF_HeroStage_Top");
                var mountainBounds = NamedRendererBounds(environment.gameObject, "DST_Mountain_04");
                var stageDepth = Vector3.Dot(mainCamera.transform.forward, stageBounds.center - mainCamera.transform.position);
                var mountainDepth = Vector3.Dot(mainCamera.transform.forward, mountainBounds.center - mainCamera.transform.position);
                Assert.That(mountainDepth, Is.GreaterThan(stageDepth), "Distant mountains must remain behind the playable terrace.");

                var distantIslandRenderer = environment.GetComponentsInChildren<Renderer>(true)
                    .First(renderer => renderer.gameObject.name == "DST_FloatingIsland_Rock");
                var distantMaterial = distantIslandRenderer.sharedMaterial;
                Assert.That(distantMaterial, Is.Not.Null);
                Assert.That(distantMaterial.shader.name, Is.EqualTo("Rasshiine/Low Poly Environment"),
                    "The shared terrace must use the texture-free facet shader instead of a flat generic Lit material.");
                var distantEmission = distantMaterial.GetColor("_EmissionColor");
                Assert.That(Mathf.Max(distantEmission.r, distantEmission.g, distantEmission.b), Is.GreaterThan(0.02f),
                    "The distant island needs a lightweight baked sky contribution so its underside does not collapse to black.");

                var loginCameraAnchor = environment.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name.Contains("CAM_LoginPanorama"));
                backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Login);
                Assert.That(Vector3.Distance(mainCamera.transform.position, loginCameraAnchor.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(mainCamera.transform.rotation, loginCameraAnchor.rotation), Is.LessThan(0.01f));
                Assert.That(mainCameraObject.GetComponent<RaidFollowCamera>().enabled, Is.False,
                    "Login must hold the authored panorama instead of reverting to the primitive fallback camera.");

                backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Battle);
                Assert.That(Vector3.Distance(mainCamera.transform.position, defaultPosition), Is.LessThan(0.001f));
                Assert.That(mainCameraObject.GetComponent<RaidFollowCamera>().enabled, Is.True, "Battle must restore its follow camera.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(mainCameraObject);
            }
        }

        [Test]
        public void ProductionScene_WiresBlenderEnvironmentAndTinyHero()
        {
            RasshiineSceneBuilder.BuildProductionScene();
            var scene = EditorSceneManager.OpenScene(
                RasshiineSceneCatalog.GetScenePath(RasshiineProductionScene.MemberHome),
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            var backdrop = Object.FindAnyObjectByType<NeonCityBackdrop>(FindObjectsInactive.Include);
            Assert.That(backdrop, Is.Not.Null);
            var serialized = new SerializedObject(backdrop);
            Assert.That(serialized.FindProperty("waypointTerracePrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("homeHeroPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("waypointTargetWidth").floatValue, Is.EqualTo(18.5f).Within(0.01f));
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Bounds NamedRendererBounds(GameObject root, string objectName)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.gameObject.name == objectName)
                .ToArray();
            Assert.That(renderers, Is.Not.Empty, objectName);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, root.name);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void AssertInViewport(Camera camera, Vector3 worldPoint, string label)
        {
            var viewport = camera.WorldToViewportPoint(worldPoint);
            Assert.That(viewport.z, Is.GreaterThan(0f), $"{label} must be in front of the camera.");
            Assert.That(viewport.x, Is.InRange(0.03f, 0.97f), $"{label} viewport x");
            Assert.That(viewport.y, Is.InRange(0.03f, 0.97f), $"{label} viewport y");
        }
    }
}
