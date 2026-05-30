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
        public const string PrototypeScenePath = "Assets/Scenes/RasshiineRaidPrototype.unity";
        public const string ProductionScenePath = "Assets/Scenes/RasshiineProduction.unity";
        public const string WebGLOutputPath = "Builds/WebGL";
        private const string BackdropRootName = "Cyberpunk Neon City Backdrop";
        private const string SkyboxMaterialDir = "Assets/Art/DesignSystem/Materials/Skybox";
        private const string AnimatedSkyboxPath = SkyboxMaterialDir + "/M_CyberRaid_AnimatedProceduralSkybox.mat";
        private const string RuntimeMaterialDir = "Assets/Art/DesignSystem/Materials/Runtime";
        private const string HeatUiRoot = "Assets/Heat - Complete Modern UI";
        private const string HeatFlatBorderDir = HeatUiRoot + "/Textures/Borders/Flat";
        private const string HeatSpecialBorderDir = HeatUiRoot + "/Textures/Borders/Special";
        private const string HeatRadial64BorderDir = HeatUiRoot + "/Textures/Borders/Radial/64px";
        private const string HeatNavigationIconDir = HeatUiRoot + "/Textures/Icons/Navigation";

        [MenuItem("AttackOnRasshiine/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Scripts/AttackOnRasshiine");
            EnsureFolder("Assets/Scripts/AttackOnRasshiine/Runtime");
            EnsureFolder(SkyboxMaterialDir);
            EnsureFolder(RuntimeMaterialDir);

            var skyboxMaterial = CreateSkyboxMaterial();
            var bossMaterial = CreateNeonMaterial("M_BossMentor_Neon", new Color(0.23f, 0.13f, 0.42f), new Color(1f, 0.2f, 0.85f), 2.2f);
            var memberMaterial = CreateNeonMaterial("M_MemberBanana_Neon", new Color(0.98f, 0.78f, 0.18f), new Color(0.1f, 0.84f, 1f), 1.35f);
            var floorMaterial = CreateUnlitMaterial("M_FloorLine_Cyan", new Color(0.1f, 0.84f, 1f, 0.7f));
            var projectileMaterial = CreateUnlitMaterial("M_Projectile_Magenta", new Color(1f, 0.2f, 0.85f, 0.85f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RasshiineRaidPrototype";
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientLight = new Color(0.03f, 0.06f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.12f);
            RenderSettings.fogDensity = 0.012f;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(RaidFollowCamera), typeof(AnimatedSkybox));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 4.6f, -9.2f);
            cameraObject.transform.rotation = Quaternion.Euler(26f, 0f, 0f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 90f;
            var followCamera = cameraObject.GetComponent<RaidFollowCamera>();
            var animatedSkybox = cameraObject.GetComponent<AnimatedSkybox>();

            var lightObject = new GameObject("Raid Key Light", typeof(Light));
            lightObject.transform.position = new Vector3(-3.4f, 8.5f, -5.2f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var keyLight = lightObject.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.64f, 0.85f, 1f);
            keyLight.intensity = 1.25f;
            RenderSettings.sun = keyLight;
            AssignAnimatedSkybox(animatedSkybox, skyboxMaterial, keyLight);

            var magentaLight = new GameObject("Magenta Rim Light", typeof(Light));
            magentaLight.transform.position = new Vector3(4.2f, 4.5f, -2.5f);
            var rim = magentaLight.GetComponent<Light>();
            rim.type = LightType.Point;
            rim.range = 18f;
            rim.intensity = 3.6f;
            rim.color = new Color(1f, 0.2f, 0.85f);

            var cyanLight = new GameObject("Cyan Portal Light", typeof(Light));
            cyanLight.transform.position = new Vector3(-4.8f, 3.1f, 2.2f);
            var portal = cyanLight.GetComponent<Light>();
            portal.type = LightType.Point;
            portal.range = 16f;
            portal.intensity = 2.8f;
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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProductionScenePath) != null)
            {
                FileUtil.ReplaceFile(PrototypeScenePath, ProductionScenePath);
            }
            else if (!AssetDatabase.CopyAsset(PrototypeScenePath, ProductionScenePath))
            {
                throw new System.Exception($"Failed to copy production scene from {PrototypeScenePath} to {ProductionScenePath}");
            }

            AssetDatabase.ImportAsset(ProductionScenePath);
            var scene = EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            scene.name = "RasshiineProduction";
            EditorSceneManager.SaveScene(scene, ProductionScenePath);
            ConfigureProductionBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built AttackOnRasshiine production scene at {ProductionScenePath}");
        }

        [MenuItem("AttackOnRasshiine/Configure Production Build Settings")]
        public static void ConfigureProductionBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ProductionScenePath, true)
            };
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
            RenderSettings.ambientLight = new Color(0.03f, 0.06f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.12f);
            RenderSettings.fogDensity = 0.012f;

            var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            var sunLight = FindDirectionalLight();
            if (sunLight != null)
            {
                RenderSettings.sun = sunLight;
            }

            AnimatedSkybox animatedSkybox = null;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                animatedSkybox = camera.GetComponent<AnimatedSkybox>();
                if (animatedSkybox == null)
                {
                    animatedSkybox = camera.gameObject.AddComponent<AnimatedSkybox>();
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

            BuildProductionScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProductionScenePath },
                locationPathName = WebGLOutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"WebGL build failed: {report.summary.result}");
            }

            Debug.Log($"WebGL build succeeded at {WebGLOutputPath} ({report.summary.totalSize / 1024f / 1024f:0.0} MB)");
        }

        private static void AssignTheme(RasshiineTheme theme, Material skyboxMaterial, Material bossMaterial, Material memberMaterial, Material floorMaterial, Material projectileMaterial)
        {
            var heatFilled = TryLoad<Sprite>(HeatFlatBorderDir + "/Flat Filled.png");
            var heatOutline3 = TryLoad<Sprite>(HeatFlatBorderDir + "/Flat Outline - 3x.png");
            var heatOutline5 = TryLoad<Sprite>(HeatFlatBorderDir + "/Flat Outline - 5x.png");
            var heatOutline10 = TryLoad<Sprite>(HeatFlatBorderDir + "/Flat Outline - 10x.png");
            var heatCrossFrame = TryLoad<Sprite>(HeatSpecialBorderDir + "/Cross Frame.png");
            var heatCrossFrameAlt = TryLoad<Sprite>(HeatSpecialBorderDir + "/Cross Frame Alt.png");
            var heatRadialFilled = TryLoad<Sprite>(HeatRadial64BorderDir + "/Radial Filled 64px.png");

            theme.UseHeatUiSkin = heatFilled != null;
            theme.PrimaryButton = heatFilled != null ? heatFilled : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Primary.png");
            theme.SecondaryButton = heatOutline5 != null ? heatOutline5 : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Secondary.png");
            theme.DangerButton = heatCrossFrameAlt != null ? heatCrossFrameAlt : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Danger.png");
            theme.RaidPanel = heatCrossFrame != null ? heatCrossFrame : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Panel_RaidFrame.png");
            theme.LogPanel = heatOutline10 != null ? heatOutline10 : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Panel_LogFrame.png");
            theme.StatCard = heatFilled != null ? heatFilled : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_StatCard.png");
            theme.ProgressFrame = heatOutline3 != null ? heatOutline3 : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Progress_Frame.png");
            theme.ProgressFillCyan = heatFilled != null ? heatFilled : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_ProgressFill_Cyan.png");
            theme.ProgressFillMagenta = heatFilled != null ? heatFilled : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_ProgressFill_Magenta.png");
            theme.HexBadge = heatRadialFilled != null ? heatRadialFilled : Load<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_HexBadge_Frame.png");
            theme.InputField = heatOutline5 != null ? heatOutline5 : theme.StatCard;
            theme.SliderFrame = theme.ProgressFrame;
            theme.SliderFill = theme.ProgressFillCyan;
            theme.SliderHandle = theme.HexBadge;
            theme.NotificationPanel = heatOutline3 != null ? heatOutline3 : theme.StatCard;
            theme.BackIcon = TryLoad<Sprite>(HeatNavigationIconDir + "/Arrow Left (64x).png");
            theme.CheckIcon = TryLoad<Sprite>(HeatNavigationIconDir + "/Checkmark (64x).png");
            theme.CloseIcon = TryLoad<Sprite>(HeatNavigationIconDir + "/Close (64x).png");
            theme.SkyboxMaterial = skyboxMaterial;
            theme.BossMaterial = bossMaterial;
            theme.MemberMaterial = memberMaterial;
            theme.FloorLineMaterial = floorMaterial;
            theme.ProjectileMaterial = projectileMaterial;
            theme.EnemyPrefab = Load<GameObject>("Assets/MyAssets/CyberSoldier/CyberSoldier.fbx");
            theme.MentorPlaceholderPrefab = Load<GameObject>("Assets/Plugins/Banana Yellow Games/Characters/Banana Man/Banana Man.fbx");
            theme.MemberPlaceholderPrefab = theme.MentorPlaceholderPrefab;
        }

        private static Material CreateSkyboxMaterial()
        {
            var retrowaveSkybox = Load<Material>("Assets/Suggo Creations/RETROWAVE SKIES Lite/Skybox Materials/Vapor_Skybox.mat");
            if (retrowaveSkybox != null)
            {
                return retrowaveSkybox;
            }

            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                shader = Shader.Find("Skybox/Panoramic");
            }

            var material = new Material(shader)
            {
                name = "M_CyberRaid_AnimatedProceduralSkybox"
            };

            if (shader != null && shader.name == "Skybox/Procedural")
            {
                SetFloatIfPresent(material, "_SunSize", 0.035f);
                SetFloatIfPresent(material, "_SunSizeConvergence", 1.8f);
                SetFloatIfPresent(material, "_AtmosphereThickness", 0.62f);
                SetFloatIfPresent(material, "_Exposure", 1.16f);
                SetFloatIfPresent(material, "_Rotation", 0f);
                SetColorIfPresent(material, "_SkyTint", new Color(0.13f, 0.48f, 0.72f, 1f));
                SetColorIfPresent(material, "_GroundColor", new Color(0.015f, 0.018f, 0.055f, 1f));
            }
            else
            {
                var texture = Load<Texture>("Assets/Art/DesignSystem/Textures/Skybox/T_CyberRaid_PanoramicSkybox.png");
                if (texture != null)
                {
                    material.SetTexture("_MainTex", texture);
                }

                SetFloatIfPresent(material, "_Exposure", 1.08f);
                SetColorIfPresent(material, "_Tint", new Color(0.9f, 0.94f, 1f, 1f));
                SetFloatIfPresent(material, "_Mapping", 1f);
                SetFloatIfPresent(material, "_ImageType", 0f);
            }

            return SaveMaterial(material, AnimatedSkyboxPath);
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

            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Street/2 lanes/road cross3.prefab", loginRoot, new Vector3(0f, -0.12f, 10.8f), new Vector3(0f, 180f, 0f), Vector3.one * 1.55f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/Skyscraper/building l 8.prefab", loginRoot, new Vector3(-7.8f, -0.1f, 13.6f), new Vector3(0f, 126f, 0f), Vector3.one * 0.78f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/Skyscraper/building sf.prefab", loginRoot, new Vector3(7.5f, -0.1f, 14.3f), new Vector3(0f, 228f, 0f), Vector3.one * 0.82f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/Shops/shop 7 B.prefab", loginRoot, new Vector3(-3.2f, -0.08f, 9.2f), new Vector3(0f, 152f, 0f), Vector3.one * 0.9f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Hologram_Noodles_01.prefab", loginRoot, new Vector3(3.4f, 2.4f, 9.8f), new Vector3(0f, 210f, 0f), Vector3.one * 1.05f, rotators, null, true);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Electric_Post.prefab", loginRoot, new Vector3(5.2f, -0.08f, 8.8f), new Vector3(0f, 205f, 0f), Vector3.one * 0.7f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/street lamp 1.prefab", loginRoot, new Vector3(-2.8f, -0.08f, 6.2f), new Vector3(0f, 180f, 0f), Vector3.one * 0.86f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/street lamp 2.prefab", loginRoot, new Vector3(2.8f, -0.08f, 6.6f), new Vector3(0f, 180f, 0f), Vector3.one * 0.86f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Particles/Rain_ParticleSystem.prefab", loginRoot, new Vector3(0f, 6.5f, 8.6f), Vector3.zero, Vector3.one * 1.8f, null, particles, false, true);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Particles/SmokeUp.prefab", loginRoot, new Vector3(-5.1f, 0.15f, 10.2f), Vector3.zero, Vector3.one * 1.2f, null, particles, false, true);

            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Street/2 lanes/road 3.prefab", homeRoot, new Vector3(0f, -0.1f, 9.4f), new Vector3(0f, 180f, 0f), Vector3.one * 1.45f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/Shops/shop 4.prefab", homeRoot, new Vector3(-6.2f, -0.08f, 9.8f), new Vector3(0f, 135f, 0f), Vector3.one * 0.88f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/Shops/shop 5.prefab", homeRoot, new Vector3(6.4f, -0.08f, 10.4f), new Vector3(0f, 222f, 0f), Vector3.one * 0.86f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/ResidentOffice/building c 3.prefab", homeRoot, new Vector3(0f, -0.08f, 15.6f), new Vector3(0f, 180f, 0f), Vector3.one * 0.75f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Bar.prefab", homeRoot, new Vector3(-2.4f, -0.08f, 7.8f), new Vector3(0f, 158f, 0f), Vector3.one * 0.82f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Electric_Post.prefab", homeRoot, new Vector3(3.4f, -0.08f, 8.2f), new Vector3(0f, 208f, 0f), Vector3.one * 0.62f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Hologram_Noodles_01.prefab", homeRoot, new Vector3(-4.2f, 2.2f, 8.8f), new Vector3(0f, 156f, 0f), Vector3.one * 0.86f, rotators, null, true);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/street lamp 1.prefab", homeRoot, new Vector3(0f, -0.08f, 5.8f), new Vector3(0f, 180f, 0f), Vector3.one * 0.82f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Particles/SmokeUp.prefab", homeRoot, new Vector3(5.4f, 0.1f, 10.4f), Vector3.zero, Vector3.one * 1f, null, particles, false, true);

            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Street/1 lane/road 1 lane direct 3.prefab", battleRoot, new Vector3(0f, -0.12f, 11.6f), new Vector3(0f, 180f, 0f), Vector3.one * 1.2f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/Skyscraper/building l 12.prefab", battleRoot, new Vector3(-8.6f, -0.08f, 17.2f), new Vector3(0f, 128f, 0f), Vector3.one * 0.65f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Buildings/ResidentOffice/building h 8.prefab", battleRoot, new Vector3(8.4f, -0.08f, 16.8f), new Vector3(0f, 230f, 0f), Vector3.one * 0.68f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Bridge_01.prefab", battleRoot, new Vector3(0f, 2.8f, 15.6f), new Vector3(0f, 180f, 0f), Vector3.one * 0.72f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/Hologram_Noodles_01.prefab", battleRoot, new Vector3(5.2f, 2.1f, 11.4f), new Vector3(0f, 214f, 0f), Vector3.one * 0.78f, rotators, null, true);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Props/traffic light 102.prefab", battleRoot, new Vector3(-5.4f, -0.08f, 10.8f), new Vector3(0f, 145f, 0f), Vector3.one * 0.76f);
            AddBackdropPrefab("Assets/BackRock-NeonCity/Prefab/Particles/Rain_ParticleSystem.prefab", battleRoot, new Vector3(0f, 6.8f, 10.2f), Vector3.zero, Vector3.one * 1.35f, null, particles, false, true);

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
            AssignObjectArray(serialized, "idleRotators", rotators);
            AssignObjectArray(serialized, "ambienceParticles", particles);
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
