using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public static class RasshiineSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/RasshiineRaidPrototype.unity";
        private const string RuntimeMaterialDir = "Assets/Art/DesignSystem/Materials/Runtime";

        [MenuItem("AttackOnRasshiine/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Scripts/AttackOnRasshiine");
            EnsureFolder("Assets/Scripts/AttackOnRasshiine/Runtime");
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

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(RaidFollowCamera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 4.6f, -9.2f);
            cameraObject.transform.rotation = Quaternion.Euler(26f, 0f, 0f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 90f;
            var followCamera = cameraObject.GetComponent<RaidFollowCamera>();

            var lightObject = new GameObject("Raid Key Light", typeof(Light));
            lightObject.transform.position = new Vector3(-3.4f, 8.5f, -5.2f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var keyLight = lightObject.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.64f, 0.85f, 1f);
            keyLight.intensity = 1.25f;

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

            var battleObject = new GameObject("Raid Battle Controller", typeof(RaidBattleController));
            battleObject.transform.SetParent(roots.transform);
            var battle = battleObject.GetComponent<RaidBattleController>();
            battle.Configure(theme, bossAnchor, partyAnchor, effectsRoot, followCamera);

            var appObject = new GameObject("Raid Game App", typeof(RaidGameApp));
            appObject.transform.SetParent(roots.transform);
            var app = appObject.GetComponent<RaidGameApp>();
            AssignSerializedObject(app, theme, battle);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Built AttackOnRasshiine prototype scene at {ScenePath}");
        }

        [MenuItem("AttackOnRasshiine/Build WebGL")]
        public static void BuildWebGL()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            BuildPrototypeScene();
            var outputPath = "Builds/WebGL";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"WebGL build failed: {report.summary.result}");
            }

            Debug.Log($"WebGL build succeeded at {outputPath} ({report.summary.totalSize / 1024f / 1024f:0.0} MB)");
        }

        private static void AssignTheme(RasshiineTheme theme, Material skyboxMaterial, Material bossMaterial, Material memberMaterial, Material floorMaterial, Material projectileMaterial)
        {
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

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                shader = Shader.Find("Skybox/Procedural");
            }

            var material = new Material(shader)
            {
                name = "M_CyberRaid_PanoramicSkybox"
            };
            var texture = Load<Texture>("Assets/Art/DesignSystem/Textures/Skybox/T_CyberRaid_PanoramicSkybox.png");
            if (texture != null)
            {
                material.SetTexture("_MainTex", texture);
            }

            material.SetFloat("_Exposure", 1.08f);
            material.SetColor("_Tint", new Color(0.9f, 0.94f, 1f, 1f));
            if (material.HasProperty("_Mapping"))
            {
                material.SetFloat("_Mapping", 1f);
            }

            if (material.HasProperty("_ImageType"))
            {
                material.SetFloat("_ImageType", 0f);
            }

            return SaveMaterial(material, "Assets/Art/DesignSystem/Materials/Skybox/M_CyberRaid_PanoramicSkybox.mat");
        }

        private static void AssignSerializedObject(RaidGameApp app, RasshiineTheme theme, RaidBattleController battle)
        {
            var serialized = new SerializedObject(app);
            serialized.FindProperty("theme").objectReferenceValue = theme;
            serialized.FindProperty("battleController").objectReferenceValue = battle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
