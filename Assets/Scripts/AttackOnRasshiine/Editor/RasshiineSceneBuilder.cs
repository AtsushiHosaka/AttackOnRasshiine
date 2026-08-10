using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public static class RasshiineSceneBuilder
    {
        public const string PrototypeScenePath = RasshiineSceneCatalog.PrototypeScenePath;
        public const string LegacyProductionScenePath = RasshiineSceneCatalog.LegacyProductionScenePath;
        public const string WebGLOutputPath = "Builds/WebGL";
        public const string VisualQaWebGLOutputPath = "Builds/WebGLVisualQa";
        public const BuildOptions ProductionWebGLBuildOptions = BuildOptions.StrictMode;
        public const BuildOptions VisualQaWebGLBuildOptions = BuildOptions.StrictMode | BuildOptions.Development;
        private const string BackdropRootName = "Cyberpunk Neon City Backdrop";
        private const string SkyboxMaterialDir = "Assets/Art/DesignSystem/Materials/Skybox";
        private const string AnimatedSkyboxPath = SkyboxMaterialDir + "/M_CyberRaid_AnimatedProceduralSkybox.mat";
        private const string LoginSkyPanoramaPath = "Assets/Art/Skyboxes/LoginOption3/LoginSky_LowPoly_Option3.png";
        private const string RuntimeMaterialDir = "Assets/Art/DesignSystem/Materials/Runtime";
        private const string HeatUiRoot = "Assets/Heat - Complete Modern UI";
        private const string HeatFlatBorderDir = HeatUiRoot + "/Textures/Borders/Flat";
        private const string HeatSpecialBorderDir = HeatUiRoot + "/Textures/Borders/Special";
        private const string HeatRadial64BorderDir = HeatUiRoot + "/Textures/Borders/Radial/64px";
        private const string HeatNavigationIconDir = RasshiineTheme.HeatNavigationIconDir;
        private const string HeatMiscIconDir = RasshiineTheme.HeatMiscIconDir;
        private const string LowPolyNatureRoot = RasshiineTheme.LowPolyNatureRoot;
        private const string NatureTreeBonusPrefabDir = RasshiineTheme.NatureTreeBonusPrefabDir;
        private const string NatureVegetationBonusPrefabDir = RasshiineTheme.NatureVegetationBonusPrefabDir;
        private const string NatureRockBonusPrefabDir = RasshiineTheme.NatureRockBonusPrefabDir;
        private const string NatureModularTerrainPrefabDir = RasshiineTheme.NatureModularTerrainPrefabDir;
        private const string NatureTreePrefabDir = RasshiineTheme.NatureTreePrefabDir;
        private const string NatureVegetationPrefabDir = RasshiineTheme.NatureVegetationPrefabDir;
        private const string NatureRockPrefabDir = RasshiineTheme.NatureRockPrefabDir;
        private const string NatureCloudPrefabDir = RasshiineTheme.NatureCloudPrefabDir;
        private const string WaypointTerracePrefabPath = "Assets/Art/WaypointTerrace/WaypointTerrace_FullEnvironment.fbx";

        public static string ProductionScenePath => RasshiineSceneCatalog.GetScenePath(RasshiineProductionScene.Boot);
        public static string[] ProductionScenePaths => RasshiineSceneCatalog.GetProductionScenePaths();

        [MenuItem("AttackOnRasshiine/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Scripts/AttackOnRasshiine");
            EnsureFolder("Assets/Scripts/AttackOnRasshiine/Runtime");
            EnsureFolder(SkyboxMaterialDir);
            EnsureFolder(RuntimeMaterialDir);

            var skyboxMaterial = CreateSkyboxMaterial();
            var bossMaterial = CreateNeonMaterial("M_BossMentor_Neon", new Color(0.13f, 0.18f, 0.38f), new Color(0.38f, 0.58f, 1f), 2.2f);
            var memberMaterial = CreateNeonMaterial("M_MemberBanana_Neon", new Color(0.98f, 0.78f, 0.18f), new Color(0.1f, 0.84f, 1f), 1.35f);
            var floorMaterial = CreateUnlitMaterial("M_FloorLine_Cyan", new Color(0.1f, 0.84f, 1f, 0.7f));
            var projectileMaterial = CreateUnlitMaterial("M_Projectile_Magenta", new Color(0.38f, 0.58f, 1f, 0.85f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RasshiineRaidPrototype";
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.43f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.24f, 0.46f, 0.62f);
            RenderSettings.fogDensity = 0.0042f;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(RaidFollowCamera), typeof(AnimatedSkybox));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.75f, -10.8f);
            cameraObject.transform.rotation = Quaternion.Euler(13f, 0f, 0f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 90f;
            var followCamera = cameraObject.GetComponent<RaidFollowCamera>();
            var animatedSkybox = cameraObject.GetComponent<AnimatedSkybox>();

            var lightObject = new GameObject("Raid Key Light", typeof(Light));
            lightObject.transform.position = new Vector3(-3.4f, 8.5f, -5.2f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var keyLight = lightObject.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.96f, 0.86f);
            keyLight.intensity = 1.12f;
            RenderSettings.sun = keyLight;
            AssignAnimatedSkybox(animatedSkybox, skyboxMaterial, keyLight);

            var rimLight = new GameObject("Blue Rim Light", typeof(Light));
            rimLight.transform.position = new Vector3(4.2f, 4.5f, -2.5f);
            var rim = rimLight.GetComponent<Light>();
            rim.type = LightType.Point;
            rim.range = 18f;
            rim.intensity = 1.8f;
            rim.color = new Color(0.38f, 0.58f, 1f);

            var cyanLight = new GameObject("Cyan Portal Light", typeof(Light));
            cyanLight.transform.position = new Vector3(-4.8f, 3.1f, 2.2f);
            var portal = cyanLight.GetComponent<Light>();
            portal.type = LightType.Point;
            portal.range = 16f;
            portal.intensity = 1.5f;
            portal.color = new Color(0.1f, 0.84f, 1f);

            var roots = new GameObject("Raid Runtime");
            var themeObject = new GameObject("Rasshiine Theme", typeof(RasshiineTheme));
            themeObject.transform.SetParent(roots.transform);
            var theme = themeObject.GetComponent<RasshiineTheme>();
            AssignTheme(theme, skyboxMaterial, bossMaterial, memberMaterial, floorMaterial, projectileMaterial);

            var bossAnchor = new GameObject("Boss Anchor").transform;
            bossAnchor.SetParent(roots.transform);
            bossAnchor.position = new Vector3(0f, 0f, 2f);

            var partyAnchor = new GameObject("Party Anchor").transform;
            partyAnchor.SetParent(roots.transform);

            var effectsRoot = new GameObject("Arena And FX").transform;
            effectsRoot.SetParent(roots.transform);
            var backdrop = CreateNeonCityBackdrop(roots.transform);

            var battleObject = new GameObject("Raid Battle Controller", typeof(RaidBattleController));
            battleObject.transform.SetParent(roots.transform);
            var battle = battleObject.GetComponent<RaidBattleController>();
            battle.Configure(theme, bossAnchor, partyAnchor, effectsRoot, followCamera);

            var appObject = new GameObject("Raid Game App", typeof(RaidGameApp));
            appObject.transform.SetParent(roots.transform);
            var app = appObject.GetComponent<RaidGameApp>();
            AssignSerializedObject(app, theme, battle, animatedSkybox, backdrop);

            EditorSceneManager.SaveScene(scene, PrototypeScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Built AttackOnRasshiine prototype scene at {PrototypeScenePath}");
        }

        [MenuItem("AttackOnRasshiine/Build Production Scene")]
        public static void BuildProductionScene()
        {
            BuildPrototypeScene();
            CreateLegacyProductionScene();
            CreateBootScene();
            CreateProductionAppScene(RasshiineProductionScene.Login);
            CreateProductionAppScene(RasshiineProductionScene.MemberHome);
            CreateProductionAppScene(RasshiineProductionScene.DevLog);
            CreateProductionAppScene(RasshiineProductionScene.MentorDashboard);
            CreateProductionAppScene(RasshiineProductionScene.Battle);
            CreateProductionAppScene(RasshiineProductionScene.FrontDisplay);
            ConfigureProductionBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Built AttackOnRasshiine production scene set");
        }

        [MenuItem("AttackOnRasshiine/Configure Production Build Settings")]
        public static void ConfigureProductionBuildSettings()
        {
            var paths = RasshiineSceneCatalog.GetProductionScenePaths();
            var scenes = new EditorBuildSettingsScene[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static void CreateBootScene()
        {
            var path = RasshiineSceneCatalog.GetScenePath(RasshiineProductionScene.Boot);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = RasshiineSceneCatalog.GetSceneName(RasshiineProductionScene.Boot);
            CreateSceneBootstrap(RasshiineProductionScene.Boot, true);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.ImportAsset(path);
        }

        private static void CreateLegacyProductionScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyProductionScenePath) != null)
            {
                FileUtil.ReplaceFile(PrototypeScenePath, LegacyProductionScenePath);
            }
            else if (!AssetDatabase.CopyAsset(PrototypeScenePath, LegacyProductionScenePath))
            {
                throw new System.Exception($"Failed to copy legacy production scene from {PrototypeScenePath} to {LegacyProductionScenePath}");
            }

            AssetDatabase.ImportAsset(LegacyProductionScenePath);
            var scene = EditorSceneManager.OpenScene(LegacyProductionScenePath, OpenSceneMode.Single);
            CreateSceneBootstrap(RasshiineProductionScene.Login, false);
            EditorSceneManager.SaveScene(scene, LegacyProductionScenePath);
            AssetDatabase.ImportAsset(LegacyProductionScenePath);
        }

        private static void CreateProductionAppScene(RasshiineProductionScene sceneId)
        {
            var path = RasshiineSceneCatalog.GetScenePath(sceneId);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                FileUtil.ReplaceFile(PrototypeScenePath, path);
            }
            else if (!AssetDatabase.CopyAsset(PrototypeScenePath, path))
            {
                throw new System.Exception($"Failed to copy production scene from {PrototypeScenePath} to {path}");
            }

            AssetDatabase.ImportAsset(path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CreateSceneBootstrap(sceneId, false);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.ImportAsset(path);
        }

        private static void CreateSceneBootstrap(RasshiineProductionScene sceneId, bool loadNextSceneOnBoot)
        {
            var existing = Object.FindAnyObjectByType<RasshiineSceneBootstrap>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var bootstrap = new GameObject("Rasshiine Scene Bootstrap", typeof(RasshiineSceneBootstrap))
                .GetComponent<RasshiineSceneBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("sceneId").enumValueIndex = (int)sceneId;
            serialized.FindProperty("bootNextScene").enumValueIndex = (int)RasshiineProductionScene.Login;
            serialized.FindProperty("loadSupabaseConfigOnBoot").boolValue = sceneId == RasshiineProductionScene.Boot;
            serialized.FindProperty("loadNextSceneOnBoot").boolValue = loadNextSceneOnBoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        [MenuItem("AttackOnRasshiine/Apply Prototype Scene Wiring")]
        public static void ApplyPrototypeSceneWiring()
        {
            EnsureFolder(SkyboxMaterialDir);
            var scene = SceneManager.GetActiveScene().path == PrototypeScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);

            var skyboxMaterial = CreateSkyboxMaterial();
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.43f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.24f, 0.46f, 0.62f);
            RenderSettings.fogDensity = 0.0042f;

            var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            var sunLight = FindDirectionalLight();
            if (sunLight != null)
            {
                sunLight.color = new Color(1f, 0.96f, 0.86f);
                sunLight.intensity = 1.12f;
                sunLight.transform.position = new Vector3(-3.4f, 8.5f, -5.2f);
                sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.sun = sunLight;
                EditorUtility.SetDirty(sunLight);
            }

            AnimatedSkybox animatedSkybox = null;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 2.75f, -10.8f);
                camera.transform.rotation = Quaternion.Euler(13f, 0f, 0f);
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.fieldOfView = 48f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 90f;
                animatedSkybox = camera.GetComponent<AnimatedSkybox>();
                if (animatedSkybox == null)
                {
                    animatedSkybox = camera.gameObject.AddComponent<AnimatedSkybox>();
                }

                var followCamera = camera.GetComponent<RaidFollowCamera>();
                if (followCamera != null)
                {
                    var serializedCamera = new SerializedObject(followCamera);
                    serializedCamera.FindProperty("overviewPosition").vector3Value = new Vector3(0f, 2.75f, -10.8f);
                    serializedCamera.FindProperty("overviewEuler").vector3Value = new Vector3(23f, 0f, 0f);
                    serializedCamera.FindProperty("followDistance").floatValue = 6.4f;
                    serializedCamera.FindProperty("followHeight").floatValue = 3.35f;
                    serializedCamera.FindProperty("followSideOffset").floatValue = 0.2f;
                    serializedCamera.FindProperty("bossLookBlend").floatValue = 0.52f;
                    serializedCamera.FindProperty("bossBackBlend").floatValue = 0.46f;
                    serializedCamera.FindProperty("centerlineBlend").floatValue = 0.42f;
                    serializedCamera.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(followCamera);
                }

                AssignAnimatedSkybox(animatedSkybox, skyboxMaterial, sunLight);
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(animatedSkybox);
            }

            var roots = GameObject.Find("Raid Runtime");
            if (roots == null)
            {
                roots = new GameObject("Raid Runtime");
            }

            var backdrop = CreateNeonCityBackdrop(roots.transform);
            var theme = Object.FindAnyObjectByType<RasshiineTheme>();
            if (theme != null)
            {
                theme.SkyboxMaterial = skyboxMaterial;
                EditorUtility.SetDirty(theme);
            }

            var app = Object.FindAnyObjectByType<RaidGameApp>();
            var battle = Object.FindAnyObjectByType<RaidBattleController>();
            if (app != null)
            {
                AssignSerializedObject(app, theme, battle, animatedSkybox, backdrop);
                EditorUtility.SetDirty(app);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Applied AttackOnRasshiine prototype scene wiring at {PrototypeScenePath}");
        }

        [MenuItem("AttackOnRasshiine/Build WebGL")]
        public static void BuildWebGL()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            ProductionWebGLSettings.Apply();
            BuildProductionScene();
            var options = new BuildPlayerOptions
            {
                scenes = RasshiineSceneCatalog.GetProductionScenePaths(),
                locationPathName = WebGLOutputPath,
                target = BuildTarget.WebGL,
                options = ProductionWebGLBuildOptions
            };
            ValidateWebGlBuildBoundary(options.locationPathName, options.options);
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"WebGL build failed: {report.summary.result}");
            }

            Debug.Log($"WebGL build succeeded at {WebGLOutputPath} ({report.summary.totalSize / 1024f / 1024f:0.0} MB)");
        }

        [MenuItem("AttackOnRasshiine/Build WebGL Visual QA (Local Only)")]
        public static void BuildWebGlVisualQa()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            ProductionWebGLSettings.Apply();
            BuildProductionScene();
            FileUtil.DeleteFileOrDirectory(VisualQaWebGLOutputPath);
            var options = new BuildPlayerOptions
            {
                scenes = RasshiineSceneCatalog.GetProductionScenePaths(),
                locationPathName = VisualQaWebGLOutputPath,
                target = BuildTarget.WebGL,
                options = VisualQaWebGLBuildOptions
            };
            ValidateWebGlBuildBoundary(options.locationPathName, options.options);
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                FileUtil.DeleteFileOrDirectory(VisualQaWebGLOutputPath);
                throw new System.Exception($"Local visual-QA WebGL build failed: {report.summary.result}");
            }

            ScrubVisualQaRuntimeConfiguration();
            Debug.Log($"Local visual-QA WebGL build succeeded at {VisualQaWebGLOutputPath} ({report.summary.totalSize / 1024f / 1024f:0.0} MB)");
        }

        public static void ValidateWebGlBuildBoundary(string outputPath, BuildOptions options)
        {
            var normalized = (outputPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            var isDevelopment = (options & BuildOptions.Development) != 0;
            if (string.Equals(normalized, WebGLOutputPath, System.StringComparison.Ordinal) && isDevelopment)
            {
                throw new System.InvalidOperationException("The production WebGL output must never contain a Development build.");
            }

            if (string.Equals(normalized, VisualQaWebGLOutputPath, System.StringComparison.Ordinal) && !isDevelopment)
            {
                throw new System.InvalidOperationException("The visual-QA output must be a Development build so its compile guard is explicit.");
            }

            if (string.Equals(WebGLOutputPath, VisualQaWebGLOutputPath, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("Production and visual-QA WebGL outputs must be physically separate.");
            }
        }

        private static void ScrubVisualQaRuntimeConfiguration()
        {
            var streamingAssetsPath = System.IO.Path.Combine(
                VisualQaWebGLOutputPath,
                "StreamingAssets");
            System.IO.Directory.CreateDirectory(streamingAssetsPath);
            var disabledConfig =
                "{\n" +
                "  \"Enabled\": false,\n" +
                "  \"SupabaseUrl\": \"\",\n" +
                "  \"SupabasePublishableKey\": \"\",\n" +
                "  \"UseDemoRepositoryFallback\": false,\n" +
                $"  \"ApiContractVersion\": \"{AttackOnRasshiine.Runtime.Services.SupabaseGameApiContract.CurrentVersion}\",\n" +
                "  \"ApiFunctionName\": \"game-api-v2\",\n" +
                "  \"ApiPath\": \"/functions/v1/game-api-v2\"\n" +
                "}\n";
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(streamingAssetsPath, "supabase-config.json"),
                disabledConfig);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(streamingAssetsPath, "supabase-config.example.json"),
                disabledConfig);
        }

        private static void AssignTheme(RasshiineTheme theme, Material skyboxMaterial, Material bossMaterial, Material memberMaterial, Material floorMaterial, Material projectileMaterial)
        {
            AssetDatabase.ImportAsset(RasshiineTheme.UiFontPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.UiTitleFontPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.UiDisplayFontPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.WaypointCompassPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.WaypointCrestPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.WaypointNavRingPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.WaypointNextRaidFramePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.WaypointParchmentPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RasshiineTheme.WaypointPlayerStatusFramePath, ImportAssetOptions.ForceUpdate);
            ConfigureLoginOption3Sprite(RasshiineTheme.LoginPanelFramePath, new Vector4(26f, 32f, 26f, 32f));
            ConfigureLoginOption3Sprite(RasshiineTheme.LoginInputFramePath, new Vector4(24f, 20f, 24f, 20f));
            ConfigureLoginOption3Sprite(RasshiineTheme.LoginStatusFramePath, new Vector4(32f, 20f, 32f, 20f));
            ConfigureLoginOption3Sprite(RasshiineTheme.LoginDividerPath, Vector4.zero);
            ConfigureLoginOption3Sprite(RasshiineTheme.LoginCtaButtonPath, new Vector4(38f, 30f, 38f, 30f));
            ConfigureLoginOption3Sprite(RasshiineTheme.LoginSunRaysPath, Vector4.zero);
            ConfigureOptionalBattlePortraitSprite(RasshiineTheme.BattlePortraitHeroBluePath);
            ConfigureOptionalBattlePortraitSprite(RasshiineTheme.BattlePortraitHeroRedPath);
            ConfigureOptionalBattlePortraitSprite(RasshiineTheme.BattlePortraitHeroGreenPath);
            ConfigureOptionalBattlePortraitSprite(RasshiineTheme.BattlePortraitBossCoralBeetlePath);

            theme.UseHeatUiSkin = true;
            theme.UseOption3LiveUi = true;
            theme.PrimaryButton = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Primary.png");
            theme.SecondaryButton = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Secondary.png");
            theme.DangerButton = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Danger.png");
            theme.RaidPanel = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Panel_RaidFrame.png");
            theme.LogPanel = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Panel_LogFrame.png");
            theme.StatCard = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_StatCard.png");
            theme.ProgressFrame = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Progress_Frame.png");
            theme.ProgressFillCyan = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_ProgressFill_Cyan.png");
            theme.ProgressFillMagenta = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_ProgressFill_Magenta.png");
            theme.HexBadge = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_HexBadge_Frame.png");
            theme.InputField = Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Input_Field.png");
            if (theme.InputField == null)
            {
                theme.InputField = theme.SecondaryButton != null ? theme.SecondaryButton : theme.StatCard;
            }
            theme.SliderFrame = theme.ProgressFrame;
            theme.SliderFill = theme.ProgressFillCyan;
            theme.SliderHandle = theme.HexBadge;
            theme.NotificationPanel = theme.StatCard;
            theme.BackIcon = TryLoad<Sprite>(HeatNavigationIconDir + "/Arrow Left (64x).png");
            theme.CheckIcon = TryLoad<Sprite>(HeatNavigationIconDir + "/Checkmark (64x).png");
            theme.CloseIcon = TryLoad<Sprite>(HeatNavigationIconDir + "/Close (64x).png");
            theme.SettingsIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Settings (64x).png");
            theme.ProductIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Box (256x).png");
            theme.AchievementIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Achievements (64x).png");
            theme.RankingIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Completed Circle (64x).png");
            theme.BattleIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Play Circle (64x).png");
            theme.HomeIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Home (64x).png");
            theme.ExternalIcon = TryLoad<Sprite>(HeatMiscIconDir + "/External (64x).png");
            theme.TeamIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Multiplayer (64x).png");
            theme.GoalIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Achievements (64x).png");
            theme.RecordIcon = TryLoad<Sprite>(HeatMiscIconDir + "/Chapters (64x).png");
            theme.BattleCircleButton = TryLoad<Sprite>(RasshiineTheme.BattleCircleButtonPath);
            theme.BattleShieldIcon = TryLoad<Sprite>(RasshiineTheme.BattleShieldIconPath);
            theme.BattlePortraitHeroBlue = TryLoad<Sprite>(RasshiineTheme.BattlePortraitHeroBluePath);
            theme.BattlePortraitHeroRed = TryLoad<Sprite>(RasshiineTheme.BattlePortraitHeroRedPath);
            theme.BattlePortraitHeroGreen = TryLoad<Sprite>(RasshiineTheme.BattlePortraitHeroGreenPath);
            theme.BattlePortraitBossCoralBeetle = TryLoad<Sprite>(RasshiineTheme.BattlePortraitBossCoralBeetlePath);
            theme.WaypointCompass = Load<Sprite>(RasshiineTheme.WaypointCompassPath);
            theme.WaypointCrest = Load<Sprite>(RasshiineTheme.WaypointCrestPath);
            theme.WaypointNavRing = Load<Sprite>(RasshiineTheme.WaypointNavRingPath);
            theme.WaypointNextRaidFrame = Load<Sprite>(RasshiineTheme.WaypointNextRaidFramePath);
            theme.WaypointParchment = Load<Sprite>(RasshiineTheme.WaypointParchmentPath);
            theme.WaypointPlayerStatusFrame = Load<Sprite>(RasshiineTheme.WaypointPlayerStatusFramePath);
            theme.LoginPanelFrame = Load<Sprite>(RasshiineTheme.LoginPanelFramePath);
            theme.LoginInputFrame = Load<Sprite>(RasshiineTheme.LoginInputFramePath);
            theme.LoginStatusFrame = Load<Sprite>(RasshiineTheme.LoginStatusFramePath);
            theme.LoginDivider = Load<Sprite>(RasshiineTheme.LoginDividerPath);
            theme.LoginCtaButton = Load<Sprite>(RasshiineTheme.LoginCtaButtonPath);
            theme.LoginSunRays = Load<Sprite>(RasshiineTheme.LoginSunRaysPath);
            theme.HeatButtonPrefab = TryLoad<GameObject>(RasshiineTheme.HeatButtonPrefabPath);
            theme.HeatInputFieldPrefab = TryLoad<GameObject>(RasshiineTheme.HeatInputFieldPrefabPath);
            theme.HeatProgressBarPrefab = TryLoad<GameObject>(RasshiineTheme.HeatProgressBarPrefabPath);
            theme.UiFont = TryLoad<Font>(RasshiineTheme.UiFontPath);
            theme.UiTitleFont = TryLoad<Font>(RasshiineTheme.UiTitleFontPath);
            theme.UiDisplayFont = TryLoad<Font>(RasshiineTheme.UiDisplayFontPath);
            theme.SkyboxMaterial = skyboxMaterial;
            theme.BossMaterial = bossMaterial;
            theme.MemberMaterial = memberMaterial;
            theme.FloorLineMaterial = floorMaterial;
            theme.ProjectileMaterial = projectileMaterial;
            theme.EnemyPrefab = Load<GameObject>("Assets/MyAssets/CyberSoldier/CyberSoldier.fbx");
            theme.MentorPlaceholderPrefab = Load<GameObject>("Assets/Plugins/Banana Yellow Games/Characters/Banana Man/Banana Man.fbx");
            theme.MemberPlaceholderPrefab = Load<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            AssignThemeNaturePrefabs(theme);
            EditorUtility.SetDirty(theme);
        }

        private static void AssignThemeNaturePrefabs(RasshiineTheme theme)
        {
            theme.BattleTerrainPrefabs = LoadOptionalAssets<GameObject>(
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_a_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_b_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_c_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/S/MT_Terrain_S_a_01.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_01.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_04.prefab",
                NatureTreeBonusPrefabDir + "/Terrain/Terrain_m_02.prefab").ToArray();
            theme.BattleHillPrefabs = LoadOptionalAssets<GameObject>(
                NatureVegetationBonusPrefabDir + "/Hills/Hill_m_01.prefab",
                NatureVegetationBonusPrefabDir + "/Hills/Hill_m_02.prefab",
                NatureVegetationBonusPrefabDir + "/Hills/Hill_s_01.prefab",
                NatureTreeBonusPrefabDir + "/Hills/Hill_s_02.prefab").ToArray();
            theme.BattleMountainPrefabs = LoadOptionalAssets<GameObject>(
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_a_02.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_b_05.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/S/MT_Mountain_S_a_01.prefab",
                NatureRockBonusPrefabDir + "/Mountains/Mountain_04.prefab",
                NatureRockBonusPrefabDir + "/Mountains/Mountain_06.prefab").ToArray();
            theme.BattleTreePrefabs = LoadOptionalAssets<GameObject>(
                NatureTreePrefabDir + "/Oak_Trees/Oak_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Oak_Trees/Oak_Tree_l_01.prefab",
                NatureTreePrefabDir + "/Simple_Trees/Simple_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Simple_Trees/Simple_Tree_m_04.prefab",
                NatureTreePrefabDir + "/Apple_Trees/Apple_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Birch_Trees/Birch_Tree_m_06.prefab").ToArray();
            theme.BattleGrassPrefabs = LoadOptionalAssets<GameObject>(
                NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_a_OneS_01.prefab",
                NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_b_OneS_02.prefab",
                NatureVegetationPrefabDir + "/Grass/GrassPlane/TwoSided/GrassPlane_a_TwoS_01.prefab",
                NatureVegetationPrefabDir + "/Bushes/Bush/Bush_a_m_01.prefab").ToArray();
            theme.BattleFlowerPrefabs = LoadOptionalAssets<GameObject>(
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_a_TwoS_01.prefab",
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_b_TwoS_03.prefab",
                NatureVegetationPrefabDir + "/Bushes/FlowerBush/FlowerBush_a_m_01.prefab").ToArray();
            theme.BattleRockPrefabs = LoadOptionalAssets<GameObject>(
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_01.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_03.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_l_2C_01.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_07.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_03.prefab").ToArray();
            theme.BattleCloudPrefabs = LoadOptionalAssets<GameObject>(
                NatureCloudPrefabDir + "/Cloud_01.prefab",
                NatureCloudPrefabDir + "/Cloud_02.prefab",
                NatureCloudPrefabDir + "/Cloud_04.prefab").ToArray();
        }

        private static Material CreateSkyboxMaterial()
        {
            ConfigureLoginSkyPanoramaImport();
            var shader = Resources.Load<Shader>("Shaders/RasshiineStylizedFantasySkybox") ??
                         Shader.Find("Rasshiine/Stylized Fantasy Skybox") ??
                         Shader.Find("Skybox/Procedural") ??
                         Shader.Find("Skybox/Panoramic");

            var material = new Material(shader)
            {
                name = "M_Rasshiine_ClearFantasySky"
            };

            if (shader != null && shader.name == "Rasshiine/Stylized Fantasy Skybox")
            {
                var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(LoginSkyPanoramaPath);
                SetTextureIfPresent(material, "_SkyPanorama", panorama);
                SetFloatIfPresent(material, "_PanoramaBlend", panorama != null ? 0.38f : 0f);
                SetFloatIfPresent(material, "_PanoramaExposure", 1.04f);
                SetColorIfPresent(material, "_PanoramaTint", new Color(1f, 0.99f, 0.96f, 1f));
                SetFloatIfPresent(material, "_Rotation", 155f);
                SetFloatIfPresent(material, "_Exposure", 1.04f);
            }
            else if (shader != null && shader.name == "Skybox/Procedural")
            {
                SetFloatIfPresent(material, "_SunSize", 0.035f);
                SetFloatIfPresent(material, "_SunSizeConvergence", 1.8f);
                SetFloatIfPresent(material, "_AtmosphereThickness", 0.84f);
                SetFloatIfPresent(material, "_Exposure", 0.88f);
                SetFloatIfPresent(material, "_Rotation", 0f);
                SetColorIfPresent(material, "_SkyTint", new Color(0.28f, 0.53f, 0.78f, 1f));
                SetColorIfPresent(material, "_GroundColor", new Color(0.08f, 0.20f, 0.31f, 1f));
            }
            else
            {
                SetFloatIfPresent(material, "_Exposure", 0.88f);
                SetColorIfPresent(material, "_Tint", new Color(0.68f, 0.84f, 1f, 1f));
            }

            return SaveMaterial(material, AnimatedSkyboxPath);
        }

        private static void ConfigureLoginSkyPanoramaImport()
        {
            AssetDatabase.ImportAsset(LoginSkyPanoramaPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(LoginSkyPanoramaPath) is not TextureImporter importer)
            {
                return;
            }

            var changed = importer.textureType != TextureImporterType.Default ||
                          importer.textureShape != TextureImporterShape.Texture2D ||
                          importer.mipmapEnabled ||
                          importer.alphaSource != TextureImporterAlphaSource.None ||
                          !importer.sRGBTexture ||
                          importer.wrapModeU != TextureWrapMode.Repeat ||
                          importer.wrapModeV != TextureWrapMode.Clamp ||
                          importer.wrapModeW != TextureWrapMode.Clamp ||
                          importer.filterMode != FilterMode.Bilinear ||
                          importer.anisoLevel != 0 ||
                          importer.maxTextureSize != 2048 ||
                          importer.textureCompression != TextureImporterCompression.Compressed ||
                          importer.isReadable;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.sRGBTexture = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.wrapModeW = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureLoginOption3Sprite(string assetPath, Vector4 border)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            var platform = importer.GetPlatformTextureSettings("WebGL");
            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single ||
                          importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                          !importer.alphaIsTransparency ||
                          importer.mipmapEnabled ||
                          importer.wrapMode != TextureWrapMode.Clamp ||
                          importer.filterMode != FilterMode.Bilinear ||
                          importer.maxTextureSize != 512 ||
                          importer.textureCompression != TextureImporterCompression.Compressed ||
                          importer.isReadable ||
                          !importer.sRGBTexture ||
                          importer.npotScale != TextureImporterNPOTScale.None ||
                          importer.spriteBorder != border ||
                          !platform.overridden ||
                          platform.maxTextureSize != 512 ||
                          platform.format != TextureImporterFormat.Automatic;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;

            platform.name = "WebGL";
            platform.overridden = true;
            platform.maxTextureSize = 512;
            platform.format = TextureImporterFormat.Automatic;
            platform.textureCompression = TextureImporterCompression.Compressed;
            platform.compressionQuality = 60;
            importer.SetPlatformTextureSettings(platform);

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureOptionalBattlePortraitSprite(string assetPath)
        {
            if (!System.IO.File.Exists(assetPath))
            {
                return;
            }

            ConfigureLoginOption3Sprite(assetPath, Vector4.zero);
        }

        private static NeonCityBackdrop CreateNeonCityBackdrop(Transform parent)
        {
            var existingBackdrop = Object.FindAnyObjectByType<NeonCityBackdrop>();
            if (existingBackdrop != null)
            {
                Object.DestroyImmediate(existingBackdrop.gameObject);
            }

            var root = new GameObject(BackdropRootName, typeof(NeonCityBackdrop));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var loginRoot = CreateBackdropRoot(root.transform, "Login Street Set");
            var homeRoot = CreateBackdropRoot(root.transform, "Home Base Set");
            var battleRoot = CreateBackdropRoot(root.transform, "Battle Backdrop Set");
            var rotators = new List<Transform>();
            var particles = new List<ParticleSystem>();

            var backdrop = root.GetComponent<NeonCityBackdrop>();
            AssignNeonCityBackdrop(backdrop, loginRoot, homeRoot, battleRoot, rotators, particles);
            backdrop.SetPreset(NeonCityBackdrop.BackdropPreset.Login);
            return backdrop;
        }

        private static Transform CreateBackdropRoot(Transform parent, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        private static GameObject AddBackdropPrefab(string path, Transform parent, Vector3 position, Vector3 rotation, Vector3 scale, List<Transform> rotators = null, List<ParticleSystem> particles = null, bool rotates = false, bool collectParticles = false)
        {
            var prefab = Load<GameObject>(path);
            if (prefab == null)
            {
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = prefab.name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(rotation);
            instance.transform.localScale = scale;
            ConfigureBackdropInstance(instance, rotates || collectParticles);

            if (rotates && rotators != null)
            {
                rotators.Add(instance.transform);
            }

            if (collectParticles && particles != null)
            {
                particles.AddRange(instance.GetComponentsInChildren<ParticleSystem>(true));
            }

            return instance;
        }

        private static void ConfigureBackdropInstance(GameObject instance, bool dynamicElement)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            foreach (var light in instance.GetComponentsInChildren<Light>(true))
            {
                light.intensity = Mathf.Min(light.intensity, 1.1f);
                light.range = Mathf.Min(light.range, 9f);
            }

            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.maxParticles = Mathf.Min(main.maxParticles, 160);
                var emission = particle.emission;
                var rate = emission.rateOverTime;
                if (rate.mode == ParticleSystemCurveMode.Constant && rate.constant > 55f)
                {
                    emission.rateOverTime = 55f;
                }
            }

            if (!dynamicElement)
            {
                GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void AssignSerializedObject(RaidGameApp app, RasshiineTheme theme, RaidBattleController battle, AnimatedSkybox animatedSkybox, NeonCityBackdrop backdrop)
        {
            var serialized = new SerializedObject(app);
            serialized.FindProperty("theme").objectReferenceValue = theme;
            serialized.FindProperty("battleController").objectReferenceValue = battle;
            serialized.FindProperty("animatedSkybox").objectReferenceValue = animatedSkybox;
            serialized.FindProperty("neonCityBackdrop").objectReferenceValue = backdrop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignAnimatedSkybox(AnimatedSkybox animatedSkybox, Material skyboxMaterial, Light sunLight)
        {
            var serialized = new SerializedObject(animatedSkybox);
            serialized.FindProperty("skyboxMaterial").objectReferenceValue = skyboxMaterial;
            serialized.FindProperty("rotationSpeed").floatValue = 2.35f;
            serialized.FindProperty("exposurePulseAmplitude").floatValue = 0.045f;
            serialized.FindProperty("exposurePulseSpeed").floatValue = 0.32f;
            serialized.FindProperty("sunLight").objectReferenceValue = sunLight;
            serialized.FindProperty("sunRotationSpeed").floatValue = 0.75f;
            serialized.FindProperty("updateAmbientProbe").boolValue = true;
            serialized.FindProperty("ambientProbeInterval").floatValue = 1.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignNeonCityBackdrop(NeonCityBackdrop backdrop, Transform loginRoot, Transform homeRoot, Transform battleRoot, IReadOnlyList<Transform> rotators, IReadOnlyList<ParticleSystem> particles)
        {
            var serialized = new SerializedObject(backdrop);
            serialized.FindProperty("initialPreset").enumValueIndex = (int)NeonCityBackdrop.BackdropPreset.Login;
            serialized.FindProperty("loginRoot").objectReferenceValue = loginRoot;
            serialized.FindProperty("homeRoot").objectReferenceValue = homeRoot;
            serialized.FindProperty("battleRoot").objectReferenceValue = battleRoot;
            serialized.FindProperty("hologramRotationSpeed").floatValue = 10f;
            serialized.FindProperty("maxParticleEmissionRate").floatValue = 55f;
            serialized.FindProperty("waypointTerracePrefab").objectReferenceValue = TryLoad<GameObject>(WaypointTerracePrefabPath);
            serialized.FindProperty("homeHeroPrefab").objectReferenceValue = TryLoad<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            serialized.FindProperty("waypointTargetWidth").floatValue = 18.5f;
            serialized.FindProperty("waypointGroundY").floatValue = -0.36f;
            serialized.FindProperty("waypointCenterZ").floatValue = 5.8f;
            AssignObjectArray(serialized, "idleRotators", rotators);
            AssignObjectArray(serialized, "ambienceParticles", particles);
            AssignObjectArray(serialized, "natureTerrainPrefabs", LoadOptionalAssets<GameObject>(
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_a_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_b_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_c_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/S/MT_Terrain_S_a_01.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_01.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_02.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_04.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_06.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_08.prefab",
                NatureTreeBonusPrefabDir + "/Terrain/Terrain_m_02.prefab"));
            AssignObjectArray(serialized, "natureHillPrefabs", LoadOptionalAssets<GameObject>(
                NatureVegetationBonusPrefabDir + "/Hills/Hill_m_01.prefab",
                NatureVegetationBonusPrefabDir + "/Hills/Hill_m_02.prefab",
                NatureVegetationBonusPrefabDir + "/Hills/Hill_s_01.prefab",
                NatureTreeBonusPrefabDir + "/Hills/Hill_s_02.prefab"));
            AssignObjectArray(serialized, "natureMountainPrefabs", LoadOptionalAssets<GameObject>(
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_a_02.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_b_05.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/S/MT_Mountain_S_a_01.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/S/MT_Mountain_S_b_10.prefab",
                NatureTreeBonusPrefabDir + "/Mountains/Mountain_01.prefab",
                NatureRockBonusPrefabDir + "/Mountains/Mountain_04.prefab",
                NatureRockBonusPrefabDir + "/Mountains/Mountain_06.prefab"));
            AssignObjectArray(serialized, "natureTreePrefabs", LoadOptionalAssets<GameObject>(
                NatureTreePrefabDir + "/Pine_Trees/TwoSided/Pine_Tree_l_01.prefab",
                NatureTreePrefabDir + "/Pine_Trees/TwoSided/Pine_Tree_m_05.prefab",
                NatureTreePrefabDir + "/Oak_Trees/Oak_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Simple_Trees/Simple_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Apple_Trees/Apple_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Birch_Trees/Birch_Tree_m_06.prefab"));
            AssignObjectArray(serialized, "natureGrassPrefabs", LoadOptionalAssets<GameObject>(
                NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_a_OneS_01.prefab",
                NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_b_OneS_02.prefab",
                NatureVegetationPrefabDir + "/Grass/GrassPlane/TwoSided/GrassPlane_a_TwoS_01.prefab",
                NatureVegetationPrefabDir + "/Grass/GrassPlane/TwoSided/GrassPlane_b_TwoS_02.prefab",
                NatureVegetationPrefabDir + "/Bushes/Bush/Bush_a_m_01.prefab"));
            AssignObjectArray(serialized, "natureFlowerPrefabs", LoadOptionalAssets<GameObject>(
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_a_TwoS_01.prefab",
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_b_TwoS_03.prefab",
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_d_TwoS_05.prefab",
                NatureVegetationPrefabDir + "/Bushes/FlowerBush/FlowerBush_a_m_01.prefab"));
            AssignObjectArray(serialized, "natureRockPrefabs", LoadOptionalAssets<GameObject>(
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_01.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_03.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_07.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_03.prefab"));
            AssignObjectArray(serialized, "natureCloudPrefabs", LoadOptionalAssets<GameObject>(
                NatureCloudPrefabDir + "/Cloud_01.prefab",
                NatureCloudPrefabDir + "/Cloud_02.prefab",
                NatureCloudPrefabDir + "/Cloud_04.prefab"));
            serialized.FindProperty("natureWaterPrefab").objectReferenceValue = TryLoad<GameObject>(NatureTreeBonusPrefabDir + "/Water/Water_01.prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<T> LoadOptionalAssets<T>(params string[] paths) where T : Object
        {
            var assets = new List<T>(paths.Length);
            for (var i = 0; i < paths.Length; i++)
            {
                var asset = TryLoad<T>(paths[i]);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        private static void AssignObjectArray<T>(SerializedObject serialized, string propertyName, IReadOnlyList<T> values) where T : Object
        {
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Light FindDirectionalLight()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetTextureIfPresent(Material material, string property, Texture value)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }

        private static Material CreateNeonMaterial(string materialName, Color baseColor, Color emissionColor, float emissionPower)
        {
            var material = new Material(FindLitShader())
            {
                name = materialName
            };
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_Color", baseColor);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor * emissionPower);
            var path = $"{RuntimeMaterialDir}/{materialName}.mat";
            return SaveMaterial(material, path);
        }

        private static Material CreateUnlitMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader)
            {
                name = materialName
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            var path = $"{RuntimeMaterialDir}/{materialName}.mat";
            return SaveMaterial(material, path);
        }

        private static Material SaveMaterial(Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                existing.name = material.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(material);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Shader FindLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"Missing asset: {path}");
            }

            return asset;
        }

        private static T TryLoad<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            var name = System.IO.Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
