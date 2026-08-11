using System;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class NeonCityBackdrop : MonoBehaviour
    {
        public enum BackdropPreset
        {
            Login,
            Home,
            Battle
        }

        [SerializeField] private BackdropPreset initialPreset = BackdropPreset.Login;
        [SerializeField] private Transform loginRoot;
        [SerializeField] private Transform homeRoot;
        [SerializeField] private Transform battleRoot;
        [SerializeField] private Transform[] idleRotators = System.Array.Empty<Transform>();
        [SerializeField] private ParticleSystem[] ambienceParticles = System.Array.Empty<ParticleSystem>();
        [SerializeField] private float hologramRotationSpeed = 10f;
        [SerializeField] private float maxParticleEmissionRate = 55f;
        [Header("Waypoint Terrace (Blender)")]
        [SerializeField] private GameObject waypointTerracePrefab;
        [SerializeField] private GameObject homeHeroPrefab;
        [SerializeField] private float waypointTargetWidth = 18.5f;
        [SerializeField] private float waypointGroundY = -0.36f;
        [SerializeField] private float waypointCenterZ = 5.8f;
        [Header("Low Poly Nature Bundle")]
        [SerializeField] private GameObject[] natureTerrainPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureHillPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureMountainPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureTreePrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureGrassPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureFlowerPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureRockPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] natureCloudPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject natureWaterPrefab;

        private BackdropPreset currentPreset;
        private bool fantasyBackdropBuilt;
        private bool sharedBackdropBuilt;
        private bool battleBackdropBuilt;
        private Camera sceneCamera;
        private RaidFollowCamera sceneFollowCamera;
        private FantasyAtmosphereDirector atmosphereDirector;
        private Transform sharedEnvironmentRoot;
        private GameObject waypointEnvironment;
        private Transform waypointCameraAnchor;
        private Transform loginCameraAnchor;
        private float waypointCameraFieldOfView = 45f;
        private float loginCameraFieldOfView = 58f;
        private Vector3 defaultCameraPosition;
        private Quaternion defaultCameraRotation;
        private float defaultCameraFieldOfView;
        private bool defaultFollowCameraEnabled;
        private bool defaultCameraPoseCaptured;
        private CosmeticInventoryDto homeHeroCosmetics;
        private static readonly Dictionary<string, Material> NatureMaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> WaypointMaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<Light, AccentLightState> BattleAccentLightStates =
            new Dictionary<Light, AccentLightState>();

        public void SetPreset(BackdropPreset preset)
        {
            currentPreset = preset;
            if (preset == BackdropPreset.Battle)
            {
                ReleaseSharedExplorationBackdrop();
            }
            else
            {
                ReleaseBattleBackdrop();
            }

            BuildFantasyBackdrop();
            SetRootActive(sharedEnvironmentRoot, preset != BackdropPreset.Battle);
            SetRootActive(loginRoot, preset == BackdropPreset.Login);
            SetRootActive(homeRoot, preset == BackdropPreset.Home);
            SetRootActive(battleRoot, preset == BackdropPreset.Battle);
            if (preset == BackdropPreset.Home)
            {
                EnsureWaypointHeroes();
            }
            ApplyCameraPreset(preset);
            ApplyLightingPreset(preset);
            ApplyAtmospherePreset(preset);
            if (preset == BackdropPreset.Battle)
            {
                // Battle must keep real sky depth and camera parallax. The old
                // camera-local panorama quad flattened the entire composition and
                // hid the procedural low-poly sky behind one static picture.
                if (sceneCamera != null)
                {
                    sceneCamera.clearFlags = CameraClearFlags.Skybox;
                }
            }
            ApplyParticleBudget();
        }

        private void ApplyAtmospherePreset(BackdropPreset preset)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CaptureDefaultCameraPose();
            if (sceneCamera == null)
            {
                return;
            }

            if (atmosphereDirector == null)
            {
                atmosphereDirector = sceneCamera.GetComponent<FantasyAtmosphereDirector>();
                if (atmosphereDirector == null)
                {
                    atmosphereDirector = sceneCamera.gameObject.AddComponent<FantasyAtmosphereDirector>();
                }
            }

            atmosphereDirector.ApplyPreset(preset);
        }

        private static void ApplyLightingPreset(BackdropPreset preset)
        {
            var isBattle = preset == BackdropPreset.Battle;
            if (!isBattle)
            {
                RestoreBattleAccentLights();
            }

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light == null)
                {
                    continue;
                }

                if (light.name is "Blue Rim Light" or "Cyan Portal Light")
                {
                    if (isBattle)
                    {
                        DisableAccentLightForBattle(light);
                    }
                    continue;
                }

                if (light.name == "Raid Key Light")
                {
                    light.gameObject.SetActive(true);
                    light.color = preset == BackdropPreset.Home
                        ? new Color(1f, 0.82f, 0.64f, 1f)
                        : new Color(1f, 0.94f, 0.82f, 1f);
                    light.intensity = preset == BackdropPreset.Home ? 0.86f : 1.05f;
                }
            }

            if (!isBattle)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.16f, 0.23f, 0.29f, 1f);
                RenderSettings.fogColor = new Color(0.15f, 0.33f, 0.50f, 1f);
                RenderSettings.fogDensity = 0.0032f;
            }
        }

        private static void DisableAccentLightForBattle(Light light)
        {
            if (!BattleAccentLightStates.ContainsKey(light))
            {
                BattleAccentLightStates.Add(light, new AccentLightState(
                    light,
                    light.gameObject.activeSelf,
                    light.enabled,
                    light.shadows));
            }

            // The cool directional fill already provides silhouette separation.
            // These wide punctual lights stacked on the arena floor and clipped its
            // albedo to white in the LDR WebGL path. Disable them only for Battle;
            // exploration presets restore the exact authored state captured above.
            light.shadows = LightShadows.None;
            light.enabled = false;
            light.gameObject.SetActive(false);
        }

        private static void RestoreBattleAccentLights()
        {
            foreach (var state in BattleAccentLightStates.Values)
            {
                if (state.Light == null)
                {
                    continue;
                }

                // Restore the GameObject before the Light component so both an
                // authored inactive object and an authored disabled component remain
                // distinguishable after the Battle preset ends.
                state.Light.gameObject.SetActive(state.GameObjectActive);
                state.Light.enabled = state.LightEnabled;
                state.Light.shadows = state.Shadows;
            }

            BattleAccentLightStates.Clear();
        }

        private readonly struct AccentLightState
        {
            public AccentLightState(
                Light light,
                bool gameObjectActive,
                bool lightEnabled,
                LightShadows shadows)
            {
                Light = light;
                GameObjectActive = gameObjectActive;
                LightEnabled = lightEnabled;
                Shadows = shadows;
            }

            public Light Light { get; }
            public bool GameObjectActive { get; }
            public bool LightEnabled { get; }
            public LightShadows Shadows { get; }
        }

        public void SetHomeHeroCosmetics(CosmeticInventoryDto inventory)
        {
            homeHeroCosmetics = inventory;
            if (homeRoot == null)
            {
                return;
            }

            foreach (var hero in homeRoot.GetComponentsInChildren<Transform>(true)
                         .Where(candidate => string.Equals(candidate.name, "Home Hero", StringComparison.Ordinal)))
            {
                TinyHeroCosmeticApplicator.Apply(hero.gameObject, homeHeroCosmetics);
            }
        }

        private void Awake()
        {
            currentPreset = IsDedicatedBattleScene() ? BackdropPreset.Battle : initialPreset;
            SetPreset(currentPreset);
        }

        private void OnEnable()
        {
            SetPreset(currentPreset);
        }

        private void Update()
        {
            var delta = hologramRotationSpeed * Time.deltaTime;
            for (var i = 0; i < idleRotators.Length; i++)
            {
                var rotator = idleRotators[i];
                if (rotator == null || !rotator.gameObject.activeInHierarchy)
                {
                    continue;
                }

                rotator.Rotate(Vector3.up, delta, Space.World);
            }
        }

        private void ApplyParticleBudget()
        {
            for (var i = 0; i < ambienceParticles.Length; i++)
            {
                var particle = ambienceParticles[i];
                if (particle == null)
                {
                    continue;
                }

                var main = particle.main;
                main.maxParticles = Mathf.Min(main.maxParticles, 160);

                var emission = particle.emission;
                var rate = emission.rateOverTime;
                if (rate.mode == ParticleSystemCurveMode.Constant && rate.constant > maxParticleEmissionRate)
                {
                    emission.rateOverTime = maxParticleEmissionRate;
                }

                if (particle.gameObject.activeInHierarchy && !particle.isPlaying)
                {
                    particle.Play();
                }
            }
        }

        private static void SetRootActive(Transform root, bool active)
        {
            if (root != null && root.gameObject.activeSelf != active)
            {
                root.gameObject.SetActive(active);
            }
        }

        private void BuildFantasyBackdrop()
        {
            BuildFantasyBackdrop(false);
        }

        private void BuildFantasyBackdrop(bool forceBuildInEditMode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !forceBuildInEditMode)
            {
                return;
            }
#endif
            if (!fantasyBackdropBuilt)
            {
                CaptureDefaultCameraPose();
                fantasyBackdropBuilt = true;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.24f, 0.46f, 0.62f, 1f);
                RenderSettings.fogDensity = 0.0042f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.34f, 0.43f, 0.52f, 1f);
            }

            if (currentPreset == BackdropPreset.Battle)
            {
                if (battleBackdropBuilt)
                {
                    return;
                }

                // RaidBattleController owns the complete playable meadow, clearing,
                // props and distant hills. Building another fantasy preset here put
                // a second ground plane, water and five primitive pyramid peaks in
                // exactly the same camera, producing the repeated dark-triangle
                // skyline seen in WebGL and wasting draw calls. Battle's backdrop
                // root is now intentionally geometry-free: this component owns only
                // the procedural sky, fog and light preset in that mode.
                ClearGeneratedBackdropChildren(battleRoot);
                HideAuthoredBackdropChildren(battleRoot);
                battleBackdropBuilt = true;
                return;
            }

            if (sharedBackdropBuilt)
            {
                return;
            }

            ClearGeneratedBackdropChildren(loginRoot);
            ClearGeneratedBackdropChildren(homeRoot);
            HideAuthoredBackdropChildren(loginRoot);
            HideAuthoredBackdropChildren(homeRoot);

            if (!TryBuildSharedWaypointTerrace())
            {
                BuildSharedExplorationFallback(new Vector3(0f, -0.18f, 1.2f));
            }
            sharedBackdropBuilt = true;
        }

        private bool IsDedicatedBattleScene()
        {
            var sceneName = gameObject.scene.name;
            return string.Equals(
                sceneName,
                RasshiineSceneCatalog.GetSceneName(RasshiineProductionScene.Battle),
                StringComparison.Ordinal);
        }

        private void ReleaseSharedExplorationBackdrop()
        {
            if (!sharedBackdropBuilt)
            {
                return;
            }

            if (sharedEnvironmentRoot != null)
            {
                DestroyGeneratedObject(sharedEnvironmentRoot.gameObject);
            }

            ClearGeneratedBackdropChildren(homeRoot);
            sharedEnvironmentRoot = null;
            waypointEnvironment = null;
            waypointCameraAnchor = null;
            loginCameraAnchor = null;
            sharedBackdropBuilt = false;
        }

        private void ReleaseBattleBackdrop()
        {
            if (!battleBackdropBuilt)
            {
                return;
            }

            ClearGeneratedBackdropChildren(battleRoot);
            battleBackdropBuilt = false;
        }

        private void BuildSharedExplorationFallback(Vector3 center)
        {
            sharedEnvironmentRoot = CreateSharedEnvironmentRoot();
            var fantasyRoot = new GameObject("Fantasy Meadow Backdrop").transform;
            fantasyRoot.SetParent(sharedEnvironmentRoot, false);
            fantasyRoot.localPosition = Vector3.zero;
            waypointEnvironment = fantasyRoot.gameObject;

            if (!TryBuildLowPolyNaturePreset(fantasyRoot, center, true, 1))
            {
                BuildGuaranteedFantasyStage(fantasyRoot, center, true);
                if (!HasAny(natureTerrainPrefabs) && !HasAny(natureTreePrefabs))
                {
                    BuildPrimitiveFantasyFallback(fantasyRoot, center, true);
                }
            }

            ScrubNatureMaterials(fantasyRoot);
            waypointCameraAnchor = CreateWaypointCameraFallback(sharedEnvironmentRoot, waypointEnvironment);
            waypointCameraFieldOfView = 48f;
            loginCameraAnchor = CreateLoginCameraFallback(sharedEnvironmentRoot, waypointEnvironment, waypointCameraAnchor);
            loginCameraFieldOfView = 60f;
        }

        private void BuildFantasyPreset(Transform root, Vector3 center, bool includeAcademy, int variant)
        {
            if (root == null)
            {
                return;
            }

            ClearGeneratedBackdropChildren(root);
            HideAuthoredBackdropChildren(root);

            var fantasyRoot = new GameObject("Fantasy Meadow Backdrop").transform;
            fantasyRoot.SetParent(root, false);
            fantasyRoot.localPosition = Vector3.zero;
            if (TryBuildLowPolyNaturePreset(fantasyRoot, center, includeAcademy, variant))
            {
                ScrubNatureMaterials(fantasyRoot, true);
                return;
            }

            BuildGuaranteedFantasyStage(fantasyRoot, center, includeAcademy);
            if (!HasAny(natureTerrainPrefabs) && !HasAny(natureTreePrefabs))
            {
                BuildPrimitiveFantasyFallback(fantasyRoot, center, includeAcademy);
            }

            ScrubNatureMaterials(fantasyRoot, true);
        }

        private bool TryBuildSharedWaypointTerrace()
        {
            if (waypointTerracePrefab == null)
            {
                return false;
            }

            sharedEnvironmentRoot = CreateSharedEnvironmentRoot();
            var environment = Instantiate(waypointTerracePrefab, sharedEnvironmentRoot, false);
            environment.name = "Waypoint Terrace Environment";
            waypointEnvironment = environment;
            FitEnvironment(environment.transform, waypointTargetWidth, waypointGroundY, waypointCenterZ);
            ConfigureWaypointRenderers(environment);
            RemapWaypointMaterials(environment);

            waypointCameraAnchor = environment.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name.Contains("CAM_WaypointTerrace", StringComparison.OrdinalIgnoreCase));
            if (waypointCameraAnchor != null)
            {
                var authoredCamera = waypointCameraAnchor.GetComponent<Camera>();
                if (authoredCamera != null)
                {
                    waypointCameraFieldOfView = authoredCamera.fieldOfView;
                    authoredCamera.enabled = false;
                }
            }
            else
            {
                waypointCameraAnchor = CreateWaypointCameraFallback(sharedEnvironmentRoot, environment);
            }

            loginCameraAnchor = environment.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name.Contains("CAM_LoginPanorama", StringComparison.OrdinalIgnoreCase));
            if (loginCameraAnchor != null)
            {
                var authoredLoginCamera = loginCameraAnchor.GetComponent<Camera>();
                if (authoredLoginCamera != null)
                {
                    loginCameraFieldOfView = authoredLoginCamera.fieldOfView;
                }
            }
            else
            {
                loginCameraAnchor = CreateLoginCameraFallback(sharedEnvironmentRoot, environment, waypointCameraAnchor);
                loginCameraFieldOfView = Mathf.Clamp(waypointCameraFieldOfView + 10f, 54f, 68f);
            }

            foreach (var authoredCamera in environment.GetComponentsInChildren<Camera>(true))
            {
                authoredCamera.enabled = false;
            }

            return true;
        }

        private void EnsureWaypointHeroes()
        {
            if (homeHeroPrefab == null || homeRoot == null || waypointEnvironment == null)
            {
                return;
            }

            var alreadySpawned = homeRoot.GetComponentsInChildren<Transform>(true)
                .Any(candidate => string.Equals(candidate.name, "Home Hero", StringComparison.Ordinal));
            if (alreadySpawned)
            {
                return;
            }

            SpawnWaypointHeroes(homeRoot, waypointEnvironment);
        }

        private Transform CreateSharedEnvironmentRoot()
        {
            if (sharedEnvironmentRoot != null)
            {
                DestroyGeneratedObject(sharedEnvironmentRoot.gameObject);
            }

            var existing = transform.Find("Shared Waypoint Terrace Backdrop");
            if (existing != null)
            {
                DestroyGeneratedObject(existing.gameObject);
            }

            var root = new GameObject("Shared Waypoint Terrace Backdrop").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        private void CaptureDefaultCameraPose()
        {
            if (defaultCameraPoseCaptured)
            {
                return;
            }

            sceneCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            if (sceneCamera == null)
            {
                return;
            }

            sceneFollowCamera = sceneCamera.GetComponent<RaidFollowCamera>();
            defaultCameraPosition = sceneCamera.transform.position;
            defaultCameraRotation = sceneCamera.transform.rotation;
            defaultCameraFieldOfView = sceneCamera.fieldOfView;
            defaultFollowCameraEnabled = sceneFollowCamera != null && sceneFollowCamera.enabled;
            defaultCameraPoseCaptured = true;
        }

        private void ApplyCameraPreset(BackdropPreset preset)
        {
            CaptureDefaultCameraPose();
            if (!defaultCameraPoseCaptured || sceneCamera == null)
            {
                return;
            }

            var authoredAnchor = preset == BackdropPreset.Login ? loginCameraAnchor : waypointCameraAnchor;
            if (preset != BackdropPreset.Battle && authoredAnchor != null)
            {
                if (sceneFollowCamera != null)
                {
                    sceneFollowCamera.enabled = false;
                }

                sceneCamera.transform.SetPositionAndRotation(authoredAnchor.position, authoredAnchor.rotation);
                sceneCamera.fieldOfView = preset == BackdropPreset.Login
                    ? loginCameraFieldOfView
                    : waypointCameraFieldOfView;
                sceneCamera.nearClipPlane = 0.05f;
                sceneCamera.farClipPlane = Mathf.Max(sceneCamera.farClipPlane, 120f);
                return;
            }

            sceneCamera.transform.SetPositionAndRotation(defaultCameraPosition, defaultCameraRotation);
            sceneCamera.fieldOfView = defaultCameraFieldOfView;
            if (sceneFollowCamera != null)
            {
                sceneFollowCamera.enabled = defaultFollowCameraEnabled;
            }
        }

        private static Transform CreateWaypointCameraFallback(Transform root, GameObject environment)
        {
            var anchor = new GameObject("Waypoint Runtime Camera Anchor").transform;
            anchor.SetParent(root, false);
            anchor.position = environment.transform.TransformPoint(new Vector3(15.2f, 11.8f, 27.2f));

            var lookTarget = environment.transform.position + Vector3.up * 1.5f;
            var hasStage = TryGetNamedRendererBounds(environment, "REF_HeroStage_Top", out var stageBounds);
            var hasWaypoint = TryGetNamedRendererBounds(environment, "WPT_Crystal_Main", out var waypointBounds);
            if (hasStage && hasWaypoint)
            {
                lookTarget = Vector3.Lerp(stageBounds.center, waypointBounds.center, 0.52f) + Vector3.up * 0.35f;
            }
            else if (hasStage)
            {
                lookTarget = stageBounds.center + Vector3.up * 0.35f;
            }

            anchor.rotation = Quaternion.LookRotation(lookTarget - anchor.position, Vector3.up);
            return anchor;
        }

        private static Transform CreateLoginCameraFallback(Transform root, GameObject environment, Transform homeAnchor)
        {
            var anchor = new GameObject("CAM_LoginPanorama_Runtime").transform;
            anchor.SetParent(root, false);

            if (homeAnchor != null)
            {
                var panoramaTarget = homeAnchor.position + homeAnchor.forward * 18f;
                anchor.position = homeAnchor.position - homeAnchor.forward * 1.8f + Vector3.up * 0.55f;
                anchor.rotation = Quaternion.LookRotation(panoramaTarget - anchor.position, Vector3.up);
                return anchor;
            }

            if (TryGetRendererBounds(environment, out var bounds))
            {
                var distance = Mathf.Max(12f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.72f);
                var lookTarget = bounds.center + Vector3.up * (bounds.extents.y * 0.12f);
                anchor.position = lookTarget + new Vector3(0f, Mathf.Max(5.5f, bounds.extents.y * 0.72f), -distance);
                anchor.rotation = Quaternion.LookRotation(lookTarget - anchor.position, Vector3.up);
                return anchor;
            }

            anchor.position = new Vector3(0f, 8.5f, -16f);
            anchor.rotation = Quaternion.LookRotation(new Vector3(0f, 1.4f, 4.5f) - anchor.position, Vector3.up);
            return anchor;
        }

        private void SpawnWaypointHeroes(Transform root, GameObject environment)
        {
            var lookTarget = environment.transform.position + Vector3.forward * 4f;
            if (TryGetNamedRendererBounds(environment, "WPT_Crystal_Main", out var waypointBounds))
            {
                lookTarget = waypointBounds.center;
            }

            if (TryGetNamedRendererBounds(environment, "REF_HeroStage_Top", out var heroStageBounds))
            {
                var heroPosition = new Vector3(heroStageBounds.center.x, heroStageBounds.max.y + 0.02f, heroStageBounds.center.z);
                if (TryProjectViewportPointToGround(new Vector2(0.23f, 0.22f), heroPosition.y, out var projectedHeroPosition))
                {
                    heroPosition = projectedHeroPosition;
                }
                SpawnHomeHero(root, "Home Hero", heroPosition, lookTarget, 1.68f, true);
            }
            else
            {
                SpawnHomeHero(root, "Home Hero", new Vector3(-4.05f, waypointGroundY + 0.03f, 1.65f), lookTarget, 1.68f, true);
            }

            if (TryGetNamedRendererBounds(environment, "STN_PathSlab_02", out var teamStageBounds))
            {
                var right = waypointCameraAnchor != null
                    ? Vector3.ProjectOnPlane(waypointCameraAnchor.right, Vector3.up).normalized
                    : Vector3.right;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.right;
                }

                var teamCenter = new Vector3(teamStageBounds.center.x, teamStageBounds.max.y + 0.01f, teamStageBounds.center.z);
                var teamA = teamCenter - right * 0.34f;
                var teamB = teamCenter;
                var teamC = teamCenter + right * 0.34f;
                if (TryProjectViewportPointToGround(new Vector2(0.44f, 0.42f), teamCenter.y, out var projectedTeamA))
                {
                    teamA = projectedTeamA;
                }
                if (TryProjectViewportPointToGround(new Vector2(0.50f, 0.42f), teamCenter.y, out var projectedTeamB))
                {
                    teamB = projectedTeamB;
                }
                if (TryProjectViewportPointToGround(new Vector2(0.56f, 0.42f), teamCenter.y, out var projectedTeamC))
                {
                    teamC = projectedTeamC;
                }
                SpawnHomeHero(root, "Team Hero A", teamA, lookTarget, 0.62f, false);
                SpawnHomeHero(root, "Team Hero B", teamB, lookTarget, 0.59f, false);
                SpawnHomeHero(root, "Team Hero C", teamC, lookTarget, 0.61f, false);
                return;
            }

            SpawnHomeHero(root, "Team Hero A", new Vector3(-0.82f, waypointGroundY + 0.03f, 5.65f), lookTarget, 0.62f, false);
            SpawnHomeHero(root, "Team Hero B", new Vector3(0.05f, waypointGroundY + 0.03f, 5.78f), lookTarget, 0.59f, false);
            SpawnHomeHero(root, "Team Hero C", new Vector3(0.92f, waypointGroundY + 0.03f, 5.62f), lookTarget, 0.61f, false);
        }

        private bool TryProjectViewportPointToGround(Vector2 viewportPoint, float groundY, out Vector3 position)
        {
            position = default;
            if (waypointCameraAnchor == null)
            {
                return false;
            }

            var authoredCamera = waypointCameraAnchor.GetComponent<Camera>();
            if (authoredCamera == null)
            {
                return false;
            }

            var ray = authoredCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            var ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (!ground.Raycast(ray, out var distance) || distance <= 0f)
            {
                return false;
            }

            position = ray.GetPoint(distance);
            return true;
        }

        private static void FitEnvironment(Transform environment, float targetWidth, float groundY, float centerZ)
        {
            environment.localPosition = Vector3.zero;
            environment.localRotation = Quaternion.identity;
            environment.localScale = Vector3.one;

            if (!TryGetRendererBounds(environment.gameObject, out var bounds))
            {
                return;
            }

            var safeWidth = Mathf.Max(0.001f, bounds.size.x);
            var scale = Mathf.Clamp(targetWidth / safeWidth, 0.01f, 10f);
            environment.localScale = Vector3.one * scale;

            if (!TryGetRendererBounds(environment.gameObject, out bounds))
            {
                return;
            }

            environment.position += new Vector3(
                -bounds.center.x,
                groundY - bounds.min.y,
                centerZ - bounds.center.z);
        }

        private void SpawnHomeHero(Transform root, string name, Vector3 worldPosition, Vector3 lookTarget, float targetHeight, bool castsShadow)
        {
            var hero = Instantiate(homeHeroPrefab, root, false);
            hero.name = name;
            TinyHeroCosmeticApplicator.Apply(
                hero,
                string.Equals(name, "Home Hero", StringComparison.Ordinal) ? homeHeroCosmetics : null);
            foreach (var animator in hero.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                if (animator.enabled && animator.gameObject.activeInHierarchy)
                {
                    animator.Update(0f);
                }
                animator.enabled = false;
            }
            hero.transform.position = worldPosition;
            hero.transform.localScale = Vector3.one;

            var lookDirection = Vector3.ProjectOnPlane(lookTarget - worldPosition, Vector3.up);
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                hero.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }

            if (TryGetRendererBounds(hero, out var bounds))
            {
                var scale = targetHeight / Mathf.Max(0.001f, bounds.size.y);
                hero.transform.localScale = Vector3.one * scale;
                if (TryGetRendererBounds(hero, out bounds))
                {
                    hero.transform.position += Vector3.up * (worldPosition.y - bounds.min.y);
                }
            }

            foreach (var renderer in hero.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = castsShadow;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            }
        }

        private static void ConfigureWaypointRenderers(GameObject environment)
        {
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                var lowerName = renderer.gameObject.name.ToLowerInvariant();
                var lightweightBackground = lowerName.Contains("cloud") || lowerName.Contains("mountain");
                renderer.shadowCastingMode = lightweightBackground ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !lowerName.Contains("cloud");
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            }
        }

        private static bool TryGetNamedRendererBounds(GameObject environment, string objectName, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static void RemapWaypointMaterials(GameObject environment)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var lowPolyLit = Resources.Load<Shader>("Shaders/RasshiineLowPolyEnvironment") ??
                             Shader.Find("Rasshiine/Low Poly Environment");
            if (urpLit == null)
            {
                return;
            }

            foreach (var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                var sourceMaterials = renderer.sharedMaterials;
                var remapped = new Material[sourceMaterials.Length];
                var changed = false;

                for (var index = 0; index < sourceMaterials.Length; index++)
                {
                    var source = sourceMaterials[index];
                    if (source == null)
                    {
                        remapped[index] = null;
                        continue;
                    }

                    var sourceKey = source.name;
                    var isDistant = renderer.gameObject.name.StartsWith("DST_", StringComparison.OrdinalIgnoreCase);
                    var key = isDistant ? $"{sourceKey}__Distant" : sourceKey;
                    if (!WaypointMaterialCache.TryGetValue(key, out var material) || material == null)
                    {
                        var isCloud = sourceKey.Contains("Cloud", StringComparison.OrdinalIgnoreCase);
                        var isMountain = sourceKey.Contains("Mountain", StringComparison.OrdinalIgnoreCase) ||
                                         sourceKey.Contains("Haze", StringComparison.OrdinalIgnoreCase);
                        var isCrystal = sourceKey.Contains("Waypoint_Crystal", StringComparison.OrdinalIgnoreCase) ||
                                        sourceKey.Contains("Waypoint_Core", StringComparison.OrdinalIgnoreCase);
                        var isWater = sourceKey.Contains("Water", StringComparison.OrdinalIgnoreCase);
                        var isWaterfall = sourceKey.Contains("Waterfall", StringComparison.OrdinalIgnoreCase);
                        var runtimeShader = isCloud && urpUnlit != null
                            ? urpUnlit
                            : lowPolyLit != null ? lowPolyLit : urpLit;
                        material = new Material(runtimeShader)
                        {
                            name = $"Runtime_{sourceKey}{(isDistant ? "_Distant" : string.Empty)}"
                        };

                        var baseColor = source.HasProperty("_BaseColor")
                            ? source.GetColor("_BaseColor")
                            : source.HasProperty("_Color")
                                ? source.GetColor("_Color")
                                : Color.white;
                        if (isMountain)
                        {
                            baseColor = Color.Lerp(baseColor, new Color(0.28f, 0.42f, 0.56f, 1f), 0.52f);
                        }
                        else if (sourceKey.Contains("HeroStage", StringComparison.OrdinalIgnoreCase) ||
                                 sourceKey.Contains("Terrace", StringComparison.OrdinalIgnoreCase) ||
                                 sourceKey.Contains("Rock", StringComparison.OrdinalIgnoreCase))
                        {
                            baseColor = Color.Lerp(baseColor, new Color(0.32f, 0.33f, 0.32f, baseColor.a), 0.32f);
                        }
                        else if (sourceKey.Contains("Grass", StringComparison.OrdinalIgnoreCase))
                        {
                            baseColor = Color.Lerp(baseColor, new Color(0.24f, 0.43f, 0.16f, baseColor.a), 0.32f);
                        }
                        else if (sourceKey.Contains("Pine", StringComparison.OrdinalIgnoreCase))
                        {
                            baseColor = Color.Lerp(baseColor, new Color(0.10f, 0.27f, 0.17f, baseColor.a), 0.32f);
                        }
                        else if (isWater)
                        {
                            baseColor = isWaterfall
                                ? new Color(0.54f, 0.86f, 0.96f, 0.94f)
                                : new Color(0.20f, 0.58f, 0.74f, 0.92f);
                        }

                        if (isDistant && !isCloud && !isWaterfall)
                        {
                            var hazeAmount = sourceKey.Contains("Grass", StringComparison.OrdinalIgnoreCase) ? 0.08f : 0.16f;
                            baseColor = Color.Lerp(baseColor, new Color(0.27f, 0.43f, 0.58f, baseColor.a), hazeAmount);
                        }
                        if (material.HasProperty("_BaseColor"))
                        {
                            material.SetColor("_BaseColor", baseColor);
                        }
                        if (material.HasProperty("_Color"))
                        {
                            material.SetColor("_Color", baseColor);
                        }
                        if (material.HasProperty("_Smoothness"))
                        {
                            var smoothness = sourceKey.Contains("Gold", StringComparison.OrdinalIgnoreCase)
                                ? 0.50f
                                : isWater
                                    ? 0.38f
                                    : 0.22f;
                            material.SetFloat("_Smoothness", smoothness);
                        }
                        if (material.HasProperty("_Metallic"))
                        {
                            material.SetFloat("_Metallic", sourceKey.Contains("Gold", StringComparison.OrdinalIgnoreCase) ? 0.34f : 0.01f);
                        }
                        var isFoliage = sourceKey.Contains("Grass", StringComparison.OrdinalIgnoreCase) ||
                                        sourceKey.Contains("Pine", StringComparison.OrdinalIgnoreCase);
                        if (material.HasProperty("_ShadowTint"))
                        {
                            // Foliage shadow tints were dark enough that, multiplied by the
                            // shader's ambient band and the foliage base color itself, the
                            // shaded side of every tree collapsed to near-black. Lifted the
                            // floor so shadow sides read as soft cool green, not harsh voids.
                            var shadowTint = sourceKey.Contains("Grass", StringComparison.OrdinalIgnoreCase)
                                ? new Color(0.30f, 0.52f, 0.38f, 1f)
                                : sourceKey.Contains("Pine", StringComparison.OrdinalIgnoreCase)
                                    ? new Color(0.26f, 0.44f, 0.38f, 1f)
                                    : sourceKey.Contains("Gold", StringComparison.OrdinalIgnoreCase)
                                        ? new Color(0.48f, 0.31f, 0.15f, 1f)
                                        : isCrystal || isWater
                                            ? new Color(0.14f, 0.42f, 0.60f, 1f)
                                            : isDistant
                                                ? new Color(0.34f, 0.48f, 0.64f, 1f)
                                                : new Color(0.25f, 0.36f, 0.54f, 1f);
                            material.SetColor("_ShadowTint", shadowTint);
                        }
                        if (material.HasProperty("_LightTint"))
                        {
                            var lightTint = isCrystal || isWater
                                ? new Color(0.66f, 0.90f, 1.0f, 1f)
                                : sourceKey.Contains("Grass", StringComparison.OrdinalIgnoreCase)
                                    ? new Color(1.0f, 0.90f, 0.64f, 1f)
                                    : new Color(1.0f, 0.82f, 0.58f, 1f);
                            material.SetColor("_LightTint", lightTint);
                        }
                        if (material.HasProperty("_RimColor"))
                        {
                            material.SetColor("_RimColor", new Color(0.24f, 0.62f, 1.0f, 1f));
                        }
                        if (material.HasProperty("_FacetSteps"))
                        {
                            material.SetFloat("_FacetSteps", sourceKey.Contains("Rock", StringComparison.OrdinalIgnoreCase) ? 3f : 4f);
                        }
                        if (material.HasProperty("_AmbientStrength"))
                        {
                            material.SetFloat("_AmbientStrength", isDistant ? 1.10f : isFoliage ? 1.05f : 0.82f);
                        }
                        if (material.HasProperty("_RimStrength"))
                        {
                            material.SetFloat("_RimStrength", isCrystal ? 0.13f : isDistant ? 0.035f : 0.07f);
                        }
                        if ((isCrystal || isWaterfall) && material.HasProperty("_EmissionColor"))
                        {
                            // Kept below 1.0 so the crystal reads as a solid faceted gem
                            // consistent with the low-poly world instead of a bloom-driven
                            // glowing gacha-game gem.
                            var emission = isWaterfall
                                ? new Color(0.10f, 0.38f, 0.48f, 1f)
                                : new Color(0.05f, 0.34f, 0.52f, 1f);
                            material.EnableKeyword("_EMISSION");
                            material.SetColor("_EmissionColor", emission);
                        }
                        else if (isDistant && material.HasProperty("_EmissionColor"))
                        {
                            // A tiny baked-in sky contribution keeps far island undersides readable
                            // without allocating another light or realtime reflection probe on WebGL.
                            var skyBounce = new Color(
                                baseColor.r * 0.24f,
                                baseColor.g * 0.29f,
                                baseColor.b * 0.36f,
                                1f);
                            material.EnableKeyword("_EMISSION");
                            material.SetColor("_EmissionColor", skyBounce);
                        }

                        WaypointMaterialCache[key] = material;
                    }

                    remapped[index] = material;
                    changed |= source != material;
                }

                if (changed)
                {
                    renderer.sharedMaterials = remapped;
                }
            }
        }

        private static void BuildGuaranteedFantasyStage(Transform fantasyRoot, Vector3 center, bool includeAcademy)
        {
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Meadow Field",
                PrimitiveType.Cube,
                center + new Vector3(0f, -0.06f, 0f),
                Vector3.zero,
                new Vector3(28f, 0.09f, 22f),
                new Color(0.53f, 0.76f, 0.42f, 1f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Near Meadow",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.01f, -1.2f),
                Vector3.zero,
                new Vector3(6.8f, 0.018f, 6.8f),
                new Color(0.62f, 0.82f, 0.43f, 1f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Hill Left",
                PrimitiveType.Sphere,
                center + new Vector3(-6.8f, 0.46f, 4.8f),
                Vector3.zero,
                new Vector3(5.8f, 1.05f, 2.2f),
                new Color(0.58f, 0.78f, 0.50f, 1f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Hill Right",
                PrimitiveType.Sphere,
                center + new Vector3(6.8f, 0.42f, 5.0f),
                Vector3.zero,
                new Vector3(6.4f, 0.95f, 2.1f),
                new Color(0.55f, 0.75f, 0.48f, 1f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Mountain Left",
                PrimitiveType.Sphere,
                center + new Vector3(-4.3f, 1.55f, 8.0f),
                Vector3.zero,
                new Vector3(3.2f, 2.3f, 1.4f),
                new Color(0.60f, 0.70f, 0.80f, 1f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Mountain Right",
                PrimitiveType.Sphere,
                center + new Vector3(4.5f, 1.70f, 8.3f),
                Vector3.zero,
                new Vector3(3.8f, 2.5f, 1.5f),
                new Color(0.58f, 0.69f, 0.80f, 1f));

            if (includeAcademy)
            {
                CreateAcademyBuilding(fantasyRoot, center + new Vector3(-3.6f, 0.92f, 3.6f), 1.24f);
                CreateAcademyBuilding(fantasyRoot, center + new Vector3(3.4f, 0.74f, 4.4f), 0.96f);
                CreateAcademyBuilding(fantasyRoot, center + new Vector3(0.2f, 0.64f, 5.7f), 0.78f);
            }

            if (includeAcademy)
            {
                CreateTree(fantasyRoot, center + new Vector3(-2.8f, 0.86f, 2.5f), 2.04f);
                CreateTree(fantasyRoot, center + new Vector3(2.9f, 0.82f, 2.7f), 1.92f);
                CreateTree(fantasyRoot, center + new Vector3(-4.7f, 0.82f, 2.2f), 1.82f);
                CreateTree(fantasyRoot, center + new Vector3(4.7f, 0.78f, 2.4f), 1.70f);
                CreateTree(fantasyRoot, center + new Vector3(-3.6f, 0.82f, 1.3f), 1.78f);
                CreateTree(fantasyRoot, center + new Vector3(3.7f, 0.76f, 1.5f), 1.66f);
                CreateTree(fantasyRoot, center + new Vector3(-5.1f, 0.66f, 0.4f), 1.38f);
                CreateTree(fantasyRoot, center + new Vector3(5.0f, 0.62f, 0.7f), 1.26f);
                CreateTree(fantasyRoot, center + new Vector3(-8.5f, 0.54f, -2.2f), 1.15f);
                CreateTree(fantasyRoot, center + new Vector3(8.4f, 0.50f, -1.8f), 1.05f);
                CreateTree(fantasyRoot, center + new Vector3(-7.4f, 0.46f, 3.0f), 0.92f);
                CreateTree(fantasyRoot, center + new Vector3(7.2f, 0.44f, 3.4f), 0.86f);
                CreateTree(fantasyRoot, center + new Vector3(-4.2f, 0.42f, 7.0f), 0.72f);
                CreateTree(fantasyRoot, center + new Vector3(4.4f, 0.42f, 7.2f), 0.70f);
            }
            else
            {
                CreateTree(fantasyRoot, center + new Vector3(-6.4f, 0.38f, 8.4f), 0.46f);
                CreateTree(fantasyRoot, center + new Vector3(6.5f, 0.38f, 8.6f), 0.44f);
                CreateTree(fantasyRoot, center + new Vector3(-3.6f, 0.36f, 9.8f), 0.36f);
                CreateTree(fantasyRoot, center + new Vector3(3.8f, 0.36f, 10.0f), 0.34f);
            }
            CreatePrimitive(fantasyRoot, "Fantasy Foreground Rock Left", PrimitiveType.Sphere, center + new Vector3(-3.0f, 0.12f, -1.6f), Vector3.zero, new Vector3(0.72f, 0.28f, 0.48f), new Color(0.64f, 0.66f, 0.58f, 1f));
            CreatePrimitive(fantasyRoot, "Fantasy Foreground Rock Right", PrimitiveType.Sphere, center + new Vector3(3.3f, 0.10f, -1.4f), Vector3.zero, new Vector3(0.62f, 0.24f, 0.42f), new Color(0.68f, 0.70f, 0.62f, 1f));
            CreatePrimitive(fantasyRoot, "Fantasy Flower Patch Left", PrimitiveType.Cylinder, center + new Vector3(-2.4f, 0.03f, -0.9f), Vector3.zero, new Vector3(0.70f, 0.018f, 0.70f), new Color(0.92f, 0.80f, 0.32f, 1f));
            CreatePrimitive(fantasyRoot, "Fantasy Flower Patch Right", PrimitiveType.Cylinder, center + new Vector3(2.5f, 0.03f, -0.7f), Vector3.zero, new Vector3(0.62f, 0.018f, 0.62f), new Color(0.82f, 0.72f, 0.94f, 1f));
        }

        private bool TryBuildLowPolyNaturePreset(Transform fantasyRoot, Vector3 center, bool includeAcademy, int variant)
        {
            var hasNatureBundle =
                HasAny(natureTerrainPrefabs) ||
                HasAny(natureHillPrefabs) ||
                HasAny(natureMountainPrefabs) ||
                HasAny(natureTreePrefabs) ||
                HasAny(natureGrassPrefabs) ||
                HasAny(natureFlowerPrefabs) ||
                HasAny(natureCloudPrefabs) ||
                natureWaterPrefab != null;

            if (!hasNatureBundle)
            {
                return false;
            }

            BuildLowPolyNatureLandscape(fantasyRoot, center, includeAcademy, variant);

            if (includeAcademy)
            {
                CreateAcademyBuilding(fantasyRoot, center + new Vector3(4.0f, 0.54f, 4.4f), 0.82f);
            }

            var treePositions = includeAcademy
                ? new[]
                {
                    new Vector3(-6.7f, 0.00f, 0.2f),
                    new Vector3(6.1f, 0.00f, 0.7f),
                    new Vector3(-7.4f, 0.00f, 2.8f),
                    new Vector3(7.0f, 0.00f, 3.5f),
                    new Vector3(-4.7f, 0.00f, 6.4f),
                    new Vector3(4.8f, 0.00f, 6.8f),
                    new Vector3(-2.9f, 0.00f, 8.0f),
                    new Vector3(2.7f, 0.00f, 8.2f)
                }
                : new[]
                {
                    new Vector3(-7.2f, 0.00f, 8.2f),
                    new Vector3(7.0f, 0.00f, 8.4f),
                    new Vector3(-4.4f, 0.00f, 9.6f),
                    new Vector3(4.7f, 0.00f, 9.8f),
                    new Vector3(-8.4f, 0.00f, 10.4f),
                    new Vector3(8.2f, 0.00f, 10.6f)
                };

            for (var i = 0; i < treePositions.Length; i++)
            {
                SpawnNaturePrefabFitted(
                    Pick(natureTreePrefabs, variant + i),
                    fantasyRoot,
                    $"Nature Tree {i + 1:00}",
                    center + treePositions[i],
                    new Vector3(0f, 33f * (variant + i), 0f),
                    includeAcademy && i < 2 ? 2.85f : 1.15f,
                    includeAcademy && i < 2 ? 2.4f : 0.95f);
            }

            var groundDetails = new[]
            {
                new Vector3(-6.4f, -0.02f, -2.8f),
                new Vector3(6.2f, -0.02f, -2.5f),
                new Vector3(-4.4f, -0.02f, 0.8f),
                new Vector3(4.8f, -0.02f, 1.0f),
                new Vector3(-2.2f, -0.02f, 4.5f),
                new Vector3(2.6f, -0.02f, 4.8f)
            };

            for (var i = 0; i < groundDetails.Length; i++)
            {
                SpawnNaturePrefab(
                    Pick(i % 2 == 0 ? natureGrassPrefabs : natureFlowerPrefabs, variant + i),
                    fantasyRoot,
                    $"Nature Meadow Detail {i + 1:00}",
                    center + groundDetails[i],
                    new Vector3(0f, -24f * i, 0f),
                    Vector3.one * 0.30f);
            }

            SpawnNaturePrefabFitted(Pick(natureRockPrefabs, variant), fantasyRoot, "Nature Rock Left", center + new Vector3(-7.1f, -0.03f, 1.2f), new Vector3(0f, 42f, 0f), 0.8f, 1.1f);
            SpawnNaturePrefabFitted(Pick(natureRockPrefabs, variant + 1), fantasyRoot, "Nature Rock Right", center + new Vector3(7.0f, -0.03f, 1.5f), new Vector3(0f, -34f, 0f), 0.75f, 1.0f);

            return true;
        }

        private void BuildLowPolyNatureLandscape(Transform fantasyRoot, Vector3 center, bool includeAcademy, int variant)
        {
            CreateLowPolyGroundPatch(fantasyRoot, "Nature Low Poly Ground Patch", center + new Vector3(0f, -0.10f, 2.5f), new Vector2(15.0f, 11.4f), new Color(0.58f, 0.77f, 0.42f, 1f));

            SpawnNaturePrefabFitted(
                natureWaterPrefab,
                fantasyRoot,
                "Nature Bundle Water Surface",
                center + new Vector3(-1.8f, 0.02f, 1.75f),
                new Vector3(0f, 8f, 0f),
                0.12f,
                5.8f);
            CreateWaterSurface(
                fantasyRoot,
                "Fantasy Shallow Creek",
                center + new Vector3(0f, 0.01f, 2.2f),
                new Vector3(8.6f, 0.02f, 2.2f),
                new Color(0.52f, 0.76f, 0.88f, 0.62f));
            CreateWaterSurface(
                fantasyRoot,
                "Fantasy Pond Left",
                center + new Vector3(-4.9f, 0.015f, 1.4f),
                new Vector3(4.8f, 0.02f, 1.85f),
                new Color(0.47f, 0.73f, 0.88f, 0.58f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Sand Bank Front",
                PrimitiveType.Cylinder,
                center + new Vector3(0.2f, 0.025f, 1.25f),
                new Vector3(0f, 0f, -4f),
                new Vector3(5.8f, 0.018f, 1.35f),
                new Color(0.82f, 0.79f, 0.54f, 1f));
            CreatePrimitive(
                fantasyRoot,
                "Fantasy Sand Bank Right",
                PrimitiveType.Cylinder,
                center + new Vector3(4.8f, 0.03f, 2.55f),
                new Vector3(0f, 0f, 5f),
                new Vector3(4.2f, 0.018f, 1.15f),
                new Color(0.84f, 0.80f, 0.55f, 1f));

            if (includeAcademy)
            {
                SpawnNaturePrefabFitted(Pick(natureCloudPrefabs, variant), fantasyRoot, "Nature Cloud Back Left", center + new Vector3(-5.2f, 4.0f, 8.6f), new Vector3(0f, 8f, 0f), 0.8f, 2.8f);
                SpawnNaturePrefabFitted(Pick(natureCloudPrefabs, variant + 1), fantasyRoot, "Nature Cloud Back Right", center + new Vector3(5.8f, 4.2f, 8.2f), new Vector3(0f, -14f, 0f), 0.8f, 3.0f);
            }
            // Battle relies on the arithmetic skybox clouds. Geometry-cloud
            // prefabs can contain large transparent cards whose bounds are not
            // representative of the visible polygon, producing pale wedges in
            // some WebGL drivers. Keeping them in exploration only avoids that
            // overdraw and artifact without removing any authored ground detail.

            if (includeAcademy)
            {
                CreateLowPolyPeak(fantasyRoot, "Fantasy Far Blue Peak Left", center + new Vector3(-6.8f, 0.12f, 7.3f), new Vector3(2.8f, 3.3f, 1.5f), 10f, new Color(0.50f, 0.66f, 0.82f, 1f));
                CreateLowPolyPeak(fantasyRoot, "Fantasy Far Blue Peak Center", center + new Vector3(-1.6f, 0.10f, 8.0f), new Vector3(3.5f, 4.2f, 1.7f), -4f, new Color(0.58f, 0.72f, 0.86f, 1f));
                CreateLowPolyPeak(fantasyRoot, "Fantasy Far Blue Peak Right", center + new Vector3(6.8f, 0.12f, 7.6f), new Vector3(3.2f, 3.8f, 1.6f), -12f, new Color(0.54f, 0.68f, 0.82f, 1f));
            }
            else
            {
                // Broader, staggered five-vertex silhouettes read as a mountain
                // range instead of three black cones. The two rear peaks are
                // softened by linear fog and add only twelve triangles in total.
                CreateLowPolyPeak(fantasyRoot, "Fantasy Battle Ridge Far Left", center + new Vector3(-4.6f, 0.02f, 15.8f), new Vector3(6.8f, 3.1f, 2.6f), 7f, new Color(0.48f, 0.62f, 0.76f, 1f));
                CreateLowPolyPeak(fantasyRoot, "Fantasy Battle Ridge Far Right", center + new Vector3(4.8f, 0.02f, 16.2f), new Vector3(6.6f, 3.0f, 2.6f), -8f, new Color(0.46f, 0.60f, 0.74f, 1f));
                CreateLowPolyPeak(fantasyRoot, "Fantasy Battle Peak Left", center + new Vector3(-7.8f, 0.08f, 11.2f), new Vector3(5.2f, 2.8f, 2.2f), 12f, new Color(0.43f, 0.57f, 0.71f, 1f));
                CreateLowPolyPeak(fantasyRoot, "Fantasy Battle Peak Center", center + new Vector3(0f, 0.06f, 13.2f), new Vector3(6.0f, 3.4f, 2.5f), -3f, new Color(0.47f, 0.61f, 0.75f, 1f));
                CreateLowPolyPeak(fantasyRoot, "Fantasy Battle Peak Right", center + new Vector3(7.9f, 0.08f, 11.5f), new Vector3(5.1f, 2.9f, 2.2f), -13f, new Color(0.44f, 0.58f, 0.72f, 1f));
            }

            SpawnNaturePrefabFitted(Pick(natureRockPrefabs, variant + 2), fantasyRoot, "Nature Ridge Rock Left", center + new Vector3(-8.3f, -0.02f, 2.1f), new Vector3(0f, -18f, 0f), 0.9f, 1.4f);
            SpawnNaturePrefabFitted(Pick(natureRockPrefabs, variant + 3), fantasyRoot, "Nature Ridge Rock Right", center + new Vector3(8.1f, -0.02f, 3.1f), new Vector3(0f, 22f, 0f), 1.0f, 1.6f);
            SpawnNaturePrefabFitted(Pick(natureRockPrefabs, variant + 4), fantasyRoot, "Nature Creek Rock Right", center + new Vector3(6.6f, -0.02f, 1.1f), new Vector3(0f, -32f, 0f), 0.65f, 1.0f);
            SpawnNaturePrefabFitted(Pick(natureRockPrefabs, variant + 5), fantasyRoot, "Nature Hill Rock Left", center + new Vector3(-5.7f, -0.02f, 4.1f), new Vector3(0f, 15f, 0f), 0.58f, 0.95f);

            if (!includeAcademy)
            {
                CreatePrimitive(
                    fantasyRoot,
                    "Fantasy Battle Distant Meadow Shelf",
                    PrimitiveType.Cube,
                    center + new Vector3(0f, 0.08f, 6.2f),
                    Vector3.zero,
                    new Vector3(12.5f, 0.10f, 1.6f),
                    new Color(0.63f, 0.80f, 0.45f, 1f));
            }
        }

        private static void BuildPrimitiveFantasyFallback(Transform fantasyRoot, Vector3 center, bool includeAcademy)
        {
            var grass = CreatePrimitive(fantasyRoot, "Soft Grass Field", PrimitiveType.Cube, center, Vector3.zero, new Vector3(18f, 0.08f, 15f), new Color(0.50f, 0.73f, 0.44f, 1f));
            grass.transform.localPosition += Vector3.down * 0.04f;

            CreatePrimitive(fantasyRoot, "Distant Hill Left", PrimitiveType.Sphere, center + new Vector3(-6.6f, 0.35f, 6.6f), Vector3.zero, new Vector3(5.6f, 1.05f, 1.6f), new Color(0.58f, 0.78f, 0.54f, 1f));
            CreatePrimitive(fantasyRoot, "Distant Hill Right", PrimitiveType.Sphere, center + new Vector3(6.8f, 0.28f, 6.9f), Vector3.zero, new Vector3(6.2f, 1.0f, 1.7f), new Color(0.55f, 0.75f, 0.53f, 1f));
            CreatePrimitive(fantasyRoot, "Blue Mountain Back", PrimitiveType.Sphere, center + new Vector3(-3.4f, 1.4f, 9.0f), Vector3.zero, new Vector3(2.8f, 2.1f, 1.0f), new Color(0.58f, 0.70f, 0.86f, 1f));
            CreatePrimitive(fantasyRoot, "Blue Mountain Far", PrimitiveType.Sphere, center + new Vector3(4.2f, 1.55f, 9.2f), Vector3.zero, new Vector3(3.4f, 2.4f, 1.1f), new Color(0.54f, 0.68f, 0.84f, 1f));

            if (includeAcademy)
            {
                CreateAcademyBuilding(fantasyRoot, center + new Vector3(-4.8f, 0.42f, 4.6f), 0.82f);
                CreateAcademyBuilding(fantasyRoot, center + new Vector3(5.1f, 0.42f, 4.8f), 0.72f);
            }

            CreateTree(fantasyRoot, center + new Vector3(-7.7f, 0.52f, 2.2f), 1.15f);
            CreateTree(fantasyRoot, center + new Vector3(7.6f, 0.48f, 2.7f), 1.05f);
            CreateTree(fantasyRoot, center + new Vector3(-5.6f, 0.44f, 6.1f), 0.82f);
            CreateTree(fantasyRoot, center + new Vector3(6.1f, 0.44f, 6.0f), 0.78f);
        }

        private static void ClearGeneratedBackdropChildren(Transform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                var child = root.GetChild(index);
                var generatedHero = string.Equals(child.name, "Home Hero", StringComparison.Ordinal) ||
                                    child.name.StartsWith("Team Hero ", StringComparison.Ordinal);
                var generatedEnvironment = string.Equals(child.name, "Waypoint Terrace Environment", StringComparison.Ordinal);
                if (child.name.StartsWith("Fantasy ", StringComparison.Ordinal) || generatedHero || generatedEnvironment)
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }
        }

        private static bool HasAny(GameObject[] prefabs)
        {
            if (prefabs == null)
            {
                return false;
            }

            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject Pick(GameObject[] prefabs, int index)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return null;
            }

            for (var offset = 0; offset < prefabs.Length; offset++)
            {
                var prefab = prefabs[Mathf.Abs(index + offset) % prefabs.Length];
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        private static GameObject SpawnNaturePrefab(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localEulerAngles = localEuler;
            instance.transform.localScale = localScale;
            ConfigureNatureInstance(instance);
            return instance;
        }

        private static GameObject SpawnNaturePrefabFootprint(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, float targetFootprint)
        {
            var instance = SpawnNaturePrefab(prefab, parent, name, localPosition, localEuler, Vector3.one);
            if (instance == null)
            {
                return null;
            }

            if (!TryGetRendererBounds(instance, out var bounds))
            {
                instance.transform.localScale = Vector3.one * 0.35f;
                return instance;
            }

            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            var scale = footprint > 0.001f ? targetFootprint / footprint : 0.35f;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 0.35f;
            }

            instance.transform.localScale *= Mathf.Clamp(scale, 0.01f, 4.0f);
            PlaceInstanceBottomAtLocalY(instance, parent, localPosition.y);
            return instance;
        }

        private static GameObject SpawnNaturePrefabFitted(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, float targetHeight, float targetFootprint)
        {
            var instance = SpawnNaturePrefab(prefab, parent, name, localPosition, localEuler, Vector3.one);
            if (instance == null)
            {
                return null;
            }

            if (!TryGetRendererBounds(instance, out var bounds))
            {
                instance.transform.localScale = Vector3.one * 0.25f;
                return instance;
            }

            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            var heightScale = bounds.size.y > 0.001f ? targetHeight / bounds.size.y : float.PositiveInfinity;
            var footprintScale = footprint > 0.001f ? targetFootprint / footprint : float.PositiveInfinity;
            var scale = Mathf.Min(heightScale, footprintScale);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 0.25f;
            }

            instance.transform.localScale *= Mathf.Clamp(scale, 0.006f, 3.2f);
            PlaceInstanceBottomAtLocalY(instance, parent, localPosition.y);
            return instance;
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static void PlaceInstanceBottomAtLocalY(GameObject instance, Transform parent, float localGroundY)
        {
            if (!TryGetRendererBounds(instance, out var bounds))
            {
                return;
            }

            var worldGroundY = parent.TransformPoint(Vector3.up * localGroundY).y;
            instance.transform.position += Vector3.up * (worldGroundY - bounds.min.y);
        }

        private static void ConfigureNatureInstance(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                DestroyGeneratedObject(collider);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.sharedMaterials = BuildNatureMaterials(instance.name, renderer.name, renderer.sharedMaterials);
            }

            foreach (var light in instance.GetComponentsInChildren<Light>(true))
            {
                light.intensity = Mathf.Min(light.intensity, 1.2f);
                light.range = Mathf.Min(light.range, 10f);
            }

            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.maxParticles = Mathf.Min(main.maxParticles, 120);
                var emission = particle.emission;
                var rate = emission.rateOverTime;
                if (rate.mode == ParticleSystemCurveMode.Constant && rate.constant > 36f)
                {
                    emission.rateOverTime = 36f;
                }
            }
        }

        private static void ScrubNatureMaterials(Transform root, bool useBattlePalette = false)
        {
            if (root == null)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = BuildNatureMaterials(
                    renderer.transform.name,
                    renderer.name,
                    renderer.sharedMaterials,
                    useBattlePalette);
            }
        }

        private static Material[] BuildNatureMaterials(
            string instanceName,
            string rendererName,
            Material[] sources,
            bool useBattlePalette = false)
        {
            if (sources == null || sources.Length == 0)
            {
                return new[]
                {
                    useBattlePalette
                        ? GetBattleNatureMaterial("grass", ColorForBattleNatureKey("grass"))
                        : GetNatureMaterial("grass", new Color(0.58f, 0.76f, 0.42f, 1f))
                };
            }

            var replacements = new Material[sources.Length];
            for (var i = 0; i < sources.Length; i++)
            {
                var sourceName = sources[i] != null ? sources[i].name : string.Empty;
                var key = GuessNatureMaterialKey(instanceName, rendererName, sourceName, i);
                replacements[i] = useBattlePalette
                    ? GetBattleNatureMaterial(key, ColorForBattleNatureKey(key))
                    : GetNatureMaterial(key, ColorForNatureKey(key));
            }

            return replacements;
        }

        private static string GuessNatureMaterialKey(string instanceName, string rendererName, string sourceName, int materialIndex)
        {
            var token = (instanceName + " " + rendererName + " " + sourceName).ToLowerInvariant();

            if (token.Contains("water") || token.Contains("lake"))
            {
                return "water";
            }

            if (token.Contains("cloud"))
            {
                return "cloud";
            }

            if (token.Contains("flower"))
            {
                return materialIndex % 2 == 0 ? "flower" : "leaf";
            }

            if (token.Contains("rock") || token.Contains("stone"))
            {
                return "rock";
            }

            if (token.Contains("mountain") || token.Contains("peak") || token.Contains("ridge"))
            {
                return "mountain";
            }

            if (token.Contains("trunk") || token.Contains("bark") || token.Contains("wood") || token.Contains("stem") || token.Contains("branch"))
            {
                return "trunk";
            }

            if (token.Contains("tree") || token.Contains("pine") || token.Contains("fir") || token.Contains("oak") || token.Contains("apple") || token.Contains("birch"))
            {
                return "leaf";
            }

            if (token.Contains("terrain") || token.Contains("hill") || token.Contains("grass") || token.Contains("bush") || token.Contains("vegetation"))
            {
                return materialIndex % 3 == 0 ? "grass" : "leaf";
            }

            return "grass";
        }

        private static Color ColorForNatureKey(string key)
        {
            switch (key)
            {
                case "water":
                    return new Color(0.47f, 0.75f, 0.93f, 1f);
                case "cloud":
                    return new Color(0.98f, 0.96f, 0.89f, 1f);
                case "flower":
                    return new Color(0.88f, 0.76f, 0.36f, 1f);
                case "rock":
                    return new Color(0.64f, 0.67f, 0.61f, 1f);
                case "mountain":
                    return new Color(0.60f, 0.70f, 0.78f, 1f);
                case "trunk":
                    return new Color(0.48f, 0.33f, 0.18f, 1f);
                case "leaf":
                    return new Color(0.42f, 0.66f, 0.31f, 1f);
                default:
                    return new Color(0.58f, 0.76f, 0.42f, 1f);
            }
        }

        private static Color ColorForBattleNatureKey(string key)
        {
            switch (key)
            {
                case "water":
                    return new Color(0.24f, 0.67f, 0.86f, 1f);
                case "cloud":
                    return new Color(0.94f, 0.96f, 0.98f, 1f);
                case "flower":
                    return new Color(0.96f, 0.61f, 0.27f, 1f);
                case "rock":
                    return new Color(0.52f, 0.55f, 0.58f, 1f);
                case "mountain":
                    return new Color(0.48f, 0.66f, 0.80f, 1f);
                case "trunk":
                    return new Color(0.43f, 0.27f, 0.14f, 1f);
                case "leaf":
                    return new Color(0.34f, 0.64f, 0.28f, 1f);
                default:
                    return new Color(0.55f, 0.72f, 0.32f, 1f);
            }
        }

        private static Material GetBattleNatureMaterial(string key, Color color)
        {
            var cacheKey = "battle-" + key;
            if (NatureMaterialCache.TryGetValue(cacheKey, out var cachedMaterial) && cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var shader = Resources.Load<Shader>("Shaders/RasshiineLowPolyEnvironment") ??
                         Shader.Find("Rasshiine/Low Poly Environment") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = "Rasshiine Battle Nature " + key
            };
            SetMaterialColor(material, color);
            ConfigureBattleNatureMaterial(material, key);
            NatureMaterialCache[cacheKey] = material;
            return material;
        }

        private static void ConfigureBattleNatureMaterial(Material material, string key)
        {
            if (material.HasProperty("_ShadowTint"))
            {
                var shadowTint = key == "water"
                    ? new Color(0.12f, 0.39f, 0.62f, 1f)
                    : key == "cloud"
                        ? new Color(0.52f, 0.65f, 0.80f, 1f)
                        : key == "mountain"
                            ? new Color(0.28f, 0.46f, 0.65f, 1f)
                            : new Color(0.18f, 0.30f, 0.42f, 1f);
                material.SetColor("_ShadowTint", shadowTint);
            }
            if (material.HasProperty("_LightTint"))
            {
                material.SetColor("_LightTint", new Color(1.0f, 0.91f, 0.75f, 1f));
            }
            if (material.HasProperty("_RimColor"))
            {
                material.SetColor("_RimColor", new Color(0.22f, 0.60f, 0.82f, 1f));
            }
            if (material.HasProperty("_FacetSteps"))
            {
                material.SetFloat("_FacetSteps", key is "rock" or "mountain" ? 3f : 4f);
            }
            if (material.HasProperty("_AmbientStrength"))
            {
                material.SetFloat("_AmbientStrength", key == "cloud" ? 0.78f : key == "mountain" ? 0.66f : 0.62f);
            }
            if (material.HasProperty("_RimStrength"))
            {
                material.SetFloat("_RimStrength", key == "water" ? 0.08f : 0.035f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Material GetNatureMaterial(string key, Color color)
        {
            if (NatureMaterialCache.TryGetValue(key, out var cachedMaterial) && cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var material = CreateMaterial(color);
            material.name = "Rasshiine Nature " + key;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", key == "water" ? 0.45f : 0.18f);
            }

            NatureMaterialCache[key] = material;
            return material;
        }

        private static void HideAuthoredBackdropChildren(Transform root)
        {
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name.StartsWith("Fantasy ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private static void CreateAcademyBuilding(Transform parent, Vector3 position, float scale)
        {
            CreatePrimitive(parent, "Fantasy Academy Body", PrimitiveType.Cube, position, Vector3.zero, new Vector3(2.0f, 1.55f, 1.35f) * scale, new Color(0.93f, 0.88f, 0.74f, 1f));
            CreatePrimitive(parent, "Fantasy Academy Roof", PrimitiveType.Cylinder, position + Vector3.up * (1.15f * scale), Vector3.zero, new Vector3(1.05f, 0.28f, 1.05f) * scale, new Color(0.28f, 0.45f, 0.76f, 1f));
            CreatePrimitive(parent, "Fantasy Academy Spire", PrimitiveType.Cylinder, position + new Vector3(0.55f, 1.45f, 0f) * scale, Vector3.zero, new Vector3(0.18f, 0.76f, 0.18f) * scale, new Color(0.86f, 0.70f, 0.36f, 1f));
        }

        private static void CreateTree(Transform parent, Vector3 position, float scale)
        {
            CreatePrimitive(parent, "Fantasy Tree Trunk", PrimitiveType.Cylinder, position, Vector3.zero, new Vector3(0.16f, 0.62f, 0.16f) * scale, new Color(0.52f, 0.36f, 0.20f, 1f));
            CreatePrimitive(parent, "Fantasy Tree Crown", PrimitiveType.Sphere, position + Vector3.up * (0.86f * scale), Vector3.zero, new Vector3(0.95f, 0.78f, 0.95f) * scale, new Color(0.38f, 0.64f, 0.32f, 1f));
            CreatePrimitive(parent, "Fantasy Tree Crown Light", PrimitiveType.Sphere, position + new Vector3(0.28f, 1.08f, -0.1f) * scale, Vector3.zero, new Vector3(0.58f, 0.46f, 0.58f) * scale, new Color(0.58f, 0.78f, 0.42f, 1f));
        }

        private static void CreateWaterSurface(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var water = CreatePrimitive(parent, name, PrimitiveType.Cylinder, position, Vector3.zero, scale, color);
            var renderer = water.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }

            var material = renderer.sharedMaterial;
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.62f);
            }
        }

        private static GameObject CreateLowPolyPeak(Transform parent, string name, Vector3 position, Vector3 scale, float yaw, Color color)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            obj.transform.localScale = scale;

            var mesh = new Mesh
            {
                name = name + " Mesh",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0f, 1f, 0f)
                },
                triangles = new[]
                {
                    0, 1, 4,
                    1, 2, 4,
                    2, 3, 4,
                    3, 0, 4,
                    0, 3, 2,
                    0, 2, 1
                }
            };
            mesh.RecalculateNormals();
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj;
        }

        private static GameObject CreateLowPolyGroundPatch(Transform parent, string name, Vector3 position, Vector2 size, Color color)
        {
            const int xSegments = 7;
            const int zSegments = 5;
            var vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            var triangles = new int[xSegments * zSegments * 6];
            for (var z = 0; z <= zSegments; z++)
            {
                for (var x = 0; x <= xSegments; x++)
                {
                    var xRatio = x / (float)xSegments - 0.5f;
                    var zRatio = z / (float)zSegments - 0.5f;
                    var y = Mathf.Sin(x * 1.37f + z * 0.73f) * 0.035f + Mathf.Cos(x * 0.61f - z * 1.19f) * 0.025f;
                    vertices[z * (xSegments + 1) + x] = new Vector3(xRatio * size.x, y, zRatio * size.y);
                }
            }

            var t = 0;
            for (var z = 0; z < zSegments; z++)
            {
                for (var x = 0; x < xSegments; x++)
                {
                    var a = z * (xSegments + 1) + x;
                    var b = a + 1;
                    var c = a + xSegments + 1;
                    var d = c + 1;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = name + " Mesh",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = GetNatureMaterial("grass", color);
            return obj;
        }

        private static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localEuler, Vector3 localScale, Color color)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localEulerAngles = localEuler;
            obj.transform.localScale = localScale;
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyGeneratedObject(collider);
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMaterial(color);
            }

            return obj;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var material = new Material(shader)
            {
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif
            UnityEngine.Object.Destroy(target);
        }
    }
}
