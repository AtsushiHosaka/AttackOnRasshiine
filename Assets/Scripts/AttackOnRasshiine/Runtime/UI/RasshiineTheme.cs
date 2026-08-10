using UnityEngine;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class RasshiineTheme : MonoBehaviour
    {
        public const string HeatButtonPrefabPath = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Button/Button.prefab";
        public const string HeatInputFieldPrefabPath = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Input Field/Input Field.prefab";
        public const string HeatProgressBarPrefabPath = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Progress Bar/Progress Bar.prefab";
        public const string HeatMiscIconDir = "Assets/Heat - Complete Modern UI/Textures/Icons/Misc";
        public const string HeatNavigationIconDir = "Assets/Heat - Complete Modern UI/Textures/Icons/Navigation";
        public const string HeatHudIconDir = "Assets/Heat - Complete Modern UI/Textures/Icons/HUD";
        public const string BattleCircleButtonPath = "Assets/Heat - Complete Modern UI/Textures/Borders/Radial/128px/Radial Filled 128px.png";
        public const string BattleShieldIconPath = HeatHudIconDir + "/Shield (64x).png";
        public const string BattlePortraitRoot = "Assets/Art/BattleUI/Portraits";
        public const string BattlePortraitHeroBluePath = BattlePortraitRoot + "/BattlePortrait_HeroBlue_v1.png";
        public const string BattlePortraitHeroRedPath = BattlePortraitRoot + "/BattlePortrait_HeroRed_v1.png";
        public const string BattlePortraitHeroGreenPath = BattlePortraitRoot + "/BattlePortrait_HeroGreen_v1.png";
        public const string BattlePortraitBossCoralBeetlePath = BattlePortraitRoot + "/BattlePortrait_BossCoralBeetle_v1.png";
        public const string TinyHeroMemberPrefabPath = "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC01.prefab";
        public const string UiFontPath = "Assets/Fonts/ZenKakuGothicNew-Medium.ttf";
        public const string UiTitleFontPath = "Assets/Fonts/ZenKakuGothicNew-Bold.ttf";
        public const string UiDisplayFontPath = "Assets/Fonts/ZenOldMincho-SemiBold.ttf";
        public const string WaypointUiRoot = "Assets/Art/WaypointUI";
        public const string WaypointCompassPath = WaypointUiRoot + "/ui_compass_primary.png";
        public const string WaypointCrestPath = WaypointUiRoot + "/ui_crest_emblem.png";
        public const string WaypointNavRingPath = WaypointUiRoot + "/ui_nav_ring_selected.png";
        public const string WaypointNextRaidFramePath = WaypointUiRoot + "/ui_next_raid_frame.png";
        public const string WaypointParchmentPath = WaypointUiRoot + "/ui_panel_parchment.png";
        public const string WaypointPlayerStatusFramePath = WaypointUiRoot + "/ui_player_status_frame.png";
        public const string LoginOption3UiRoot = "Assets/Art/UI/LoginOption3";
        public const string LoginPanelFramePath = LoginOption3UiRoot + "/login_panel_frame.png";
        public const string LoginInputFramePath = LoginOption3UiRoot + "/login_input_frame.png";
        public const string LoginStatusFramePath = LoginOption3UiRoot + "/login_status_frame.png";
        public const string LoginDividerPath = LoginOption3UiRoot + "/login_divider.png";
        public const string LoginCtaButtonPath = LoginOption3UiRoot + "/login_cta_button.png";
        public const string LoginSunRaysPath = LoginOption3UiRoot + "/login_sun_rays.png";
        public const string LowPolyNatureRoot = "Assets/LMHPOLY/Low Poly Nature Bundle";
        public const string NatureModularTerrainPrefabDir = LowPolyNatureRoot + "/Modular Terrain/Terrain_Assets/Prefabs";
        public const string NatureTreePrefabDir = LowPolyNatureRoot + "/Trees/Tree Assets/Prefabs/Trees/NoLOD/No_Bottoms/Mesh_Colliders";
        public const string NatureVegetationPrefabDir = LowPolyNatureRoot + "/Vegetation/Vegetation Assets/Prefabs";
        public const string NatureVegetationBonusPrefabDir = LowPolyNatureRoot + "/Vegetation/Bonus Assets/Prefabs";
        public const string NatureTreeBonusPrefabDir = LowPolyNatureRoot + "/Trees/Bonus Assets/Prefabs";
        public const string NatureRockPrefabDir = LowPolyNatureRoot + "/Rocks/Rock Assets/Prefabs/With_Box_Colliders/2 Color";
        public const string NatureRockBonusPrefabDir = LowPolyNatureRoot + "/Rocks/Bonus Assets/Prefabs";
        public const string NatureCloudPrefabDir = LowPolyNatureRoot + "/_Bonus Assets/Prefabs/Clouds";

        [Header("Sprites")]
        public bool UseHeatUiSkin;
        [Tooltip("Use the approved option-3 navy/gold frame assets with live text across every production screen.")]
        public bool UseOption3LiveUi = true;
        public Sprite PrimaryButton;
        public Sprite SecondaryButton;
        public Sprite DangerButton;
        public Sprite RaidPanel;
        public Sprite LogPanel;
        public Sprite StatCard;
        public Sprite ProgressFrame;
        public Sprite ProgressFillCyan;
        public Sprite ProgressFillMagenta;
        public Sprite HexBadge;
        public Sprite InputField;
        public Sprite SliderFrame;
        public Sprite SliderFill;
        public Sprite SliderHandle;
        public Sprite NotificationPanel;
        public Sprite BackIcon;
        public Sprite CheckIcon;
        public Sprite CloseIcon;
        public Sprite SettingsIcon;
        public Sprite ProductIcon;
        public Sprite AchievementIcon;
        public Sprite RankingIcon;
        public Sprite BattleIcon;
        public Sprite HomeIcon;
        public Sprite ExternalIcon;
        public Sprite TeamIcon;
        public Sprite GoalIcon;
        public Sprite RecordIcon;
        public Sprite BattleCircleButton;
        public Sprite BattleShieldIcon;

        [Header("Battle Portraits")]
        public Sprite BattlePortraitHeroBlue;
        public Sprite BattlePortraitHeroRed;
        public Sprite BattlePortraitHeroGreen;
        public Sprite BattlePortraitBossCoralBeetle;

        [Header("Waypoint Terrace UI")]
        public Sprite WaypointCompass;
        public Sprite WaypointCrest;
        public Sprite WaypointNavRing;
        public Sprite WaypointNextRaidFrame;
        public Sprite WaypointParchment;
        public Sprite WaypointPlayerStatusFrame;

        [Header("Login Option 3 UI")]
        public Sprite LoginPanelFrame;
        public Sprite LoginInputFrame;
        public Sprite LoginStatusFrame;
        public Sprite LoginDivider;
        public Sprite LoginCtaButton;
        public Sprite LoginSunRays;

        [Header("Typography")]
        public Font UiFont;
        public Font UiTitleFont;
        public Font UiDisplayFont;

        [Header("Materials")]
        public Material SkyboxMaterial;
        public Material BossMaterial;
        public Material MemberMaterial;
        public Material FloorLineMaterial;
        public Material ProjectileMaterial;

        [Header("Model placeholders")]
        public GameObject EnemyPrefab;
        public GameObject MentorPlaceholderPrefab;
        public GameObject MemberPlaceholderPrefab;

        [Header("Low Poly Nature Bundle")]
        public GameObject[] BattleTerrainPrefabs;
        public GameObject[] BattleHillPrefabs;
        public GameObject[] BattleMountainPrefabs;
        public GameObject[] BattleTreePrefabs;
        public GameObject[] BattleGrassPrefabs;
        public GameObject[] BattleFlowerPrefabs;
        public GameObject[] BattleRockPrefabs;
        public GameObject[] BattleCloudPrefabs;

        [Header("Heat UI Prefabs")]
        public GameObject HeatButtonPrefab;
        public GameObject HeatInputFieldPrefab;
        public GameObject HeatProgressBarPrefab;

        // Waypoint Terrace tokens. Legacy property names remain for compatibility with
        // established screens, but their values now map to the approved fantasy palette.
        public readonly Color Void = new(0.067f, 0.157f, 0.267f, 1f); // Ink / Night
        public readonly Color Panel = new(0.067f, 0.157f, 0.267f, 0.9f);
        public readonly Color Cyan = new(0.400f, 0.847f, 0.949f, 1f); // Sky / Cyan
        public readonly Color Magenta = new(0.843f, 0.737f, 0.447f, 1f); // Gold / Main
        public readonly Color Purple = new(0.110f, 0.239f, 0.369f, 1f); // Ink / Blue
        public readonly Color Mint = new(0.459f, 0.725f, 0.537f, 1f);
        public readonly Color Gold = new(0.843f, 0.737f, 0.447f, 1f);
        public readonly Color Text = new(0.965f, 0.941f, 0.875f, 1f); // Ivory
        public readonly Color MutedText = new(0.741f, 0.925f, 0.969f, 0.86f); // Sky / Pale
        public readonly Color Warning = new(0.839f, 0.651f, 0.341f, 1f);
        public readonly Color Danger = new(0.780f, 0.427f, 0.447f, 1f);

#if UNITY_EDITOR
        private void Awake()
        {
            ResolveHeatPrefabsInEditor();
        }

        private void OnValidate()
        {
            ResolveHeatPrefabsInEditor();
        }

        private void ResolveHeatPrefabsInEditor()
        {
            HeatButtonPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HeatButtonPrefabPath);
            HeatInputFieldPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HeatInputFieldPrefabPath);
            HeatProgressBarPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HeatProgressBarPrefabPath);
            MemberPlaceholderPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TinyHeroMemberPrefabPath);
            UiFont ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            UiTitleFont ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UiTitleFontPath);
            UiDisplayFont ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UiDisplayFontPath);
            BackIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatNavigationIconDir + "/Arrow Left (64x).png");
            CheckIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatNavigationIconDir + "/Checkmark (64x).png");
            CloseIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatNavigationIconDir + "/Close (64x).png");
            SettingsIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Settings (64x).png");
            ProductIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Box (256x).png");
            AchievementIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Achievements (64x).png");
            RankingIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Completed Circle (64x).png");
            BattleIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Play Circle (64x).png");
            HomeIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Home (64x).png");
            ExternalIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/External (64x).png");
            TeamIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Multiplayer (64x).png");
            GoalIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Achievements (64x).png");
            RecordIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(HeatMiscIconDir + "/Chapters (64x).png");
            BattleCircleButton ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BattleCircleButtonPath);
            BattleShieldIcon ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BattleShieldIconPath);
            BattlePortraitHeroBlue ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BattlePortraitHeroBluePath);
            BattlePortraitHeroRed ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BattlePortraitHeroRedPath);
            BattlePortraitHeroGreen ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BattlePortraitHeroGreenPath);
            BattlePortraitBossCoralBeetle ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BattlePortraitBossCoralBeetlePath);
            WaypointCompass ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WaypointCompassPath);
            WaypointCrest ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WaypointCrestPath);
            WaypointNavRing ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WaypointNavRingPath);
            WaypointNextRaidFrame ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WaypointNextRaidFramePath);
            WaypointParchment ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WaypointParchmentPath);
            WaypointPlayerStatusFrame ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WaypointPlayerStatusFramePath);
            LoginPanelFrame ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LoginPanelFramePath);
            LoginInputFrame ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LoginInputFramePath);
            LoginStatusFrame ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LoginStatusFramePath);
            LoginDivider ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LoginDividerPath);
            LoginCtaButton ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LoginCtaButtonPath);
            LoginSunRays ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LoginSunRaysPath);
            BattleTerrainPrefabs = ResolveOptionalPrefabs(BattleTerrainPrefabs,
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_a_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_b_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_c_01.prefab",
                NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/S/MT_Terrain_S_a_01.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_01.prefab",
                NatureVegetationBonusPrefabDir + "/Terrain/Terrain_m_04.prefab",
                NatureTreeBonusPrefabDir + "/Terrain/Terrain_m_02.prefab");
            BattleHillPrefabs = ResolveOptionalPrefabs(BattleHillPrefabs,
                NatureVegetationBonusPrefabDir + "/Hills/Hill_m_01.prefab",
                NatureVegetationBonusPrefabDir + "/Hills/Hill_m_02.prefab",
                NatureVegetationBonusPrefabDir + "/Hills/Hill_s_01.prefab",
                NatureTreeBonusPrefabDir + "/Hills/Hill_s_02.prefab");
            BattleMountainPrefabs = ResolveOptionalPrefabs(BattleMountainPrefabs,
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_a_02.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_b_05.prefab",
                NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/S/MT_Mountain_S_a_01.prefab",
                NatureRockBonusPrefabDir + "/Mountains/Mountain_04.prefab",
                NatureRockBonusPrefabDir + "/Mountains/Mountain_06.prefab");
            BattleTreePrefabs = ResolveOptionalPrefabs(BattleTreePrefabs,
                NatureTreePrefabDir + "/Oak_Trees/Oak_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Oak_Trees/Oak_Tree_l_01.prefab",
                NatureTreePrefabDir + "/Simple_Trees/Simple_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Simple_Trees/Simple_Tree_m_04.prefab",
                NatureTreePrefabDir + "/Apple_Trees/Apple_Tree_m_01.prefab",
                NatureTreePrefabDir + "/Birch_Trees/Birch_Tree_m_06.prefab");
            BattleGrassPrefabs = ResolveOptionalPrefabs(BattleGrassPrefabs,
                NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_a_OneS_01.prefab",
                NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_b_OneS_02.prefab",
                NatureVegetationPrefabDir + "/Grass/GrassPlane/TwoSided/GrassPlane_a_TwoS_01.prefab",
                NatureVegetationPrefabDir + "/Bushes/Bush/Bush_a_m_01.prefab");
            BattleFlowerPrefabs = ResolveOptionalPrefabs(BattleFlowerPrefabs,
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_a_TwoS_01.prefab",
                NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_b_TwoS_03.prefab",
                NatureVegetationPrefabDir + "/Bushes/FlowerBush/FlowerBush_a_m_01.prefab");
            BattleRockPrefabs = ResolveOptionalPrefabs(BattleRockPrefabs,
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_01.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_03.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_l_2C_01.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_07.prefab",
                NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_03.prefab");
            BattleCloudPrefabs = ResolveOptionalPrefabs(BattleCloudPrefabs,
                NatureCloudPrefabDir + "/Cloud_01.prefab",
                NatureCloudPrefabDir + "/Cloud_02.prefab",
                NatureCloudPrefabDir + "/Cloud_04.prefab");
        }

        private static GameObject[] ResolveOptionalPrefabs(GameObject[] existing, params string[] paths)
        {
            if (existing != null)
            {
                var hasExistingPrefab = false;
                var containsUnsupportedRock = false;
                for (var i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null)
                    {
                        hasExistingPrefab = true;
                        var assetPath = UnityEditor.AssetDatabase.GetAssetPath(existing[i]);
                        if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("/Flat_Rocks/"))
                        {
                            containsUnsupportedRock = true;
                            break;
                        }
                    }
                }

                if (hasExistingPrefab && !containsUnsupportedRock)
                {
                    return existing;
                }
            }

            var prefabs = new System.Collections.Generic.List<GameObject>(paths.Length);
            for (var i = 0; i < paths.Length; i++)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            return prefabs.ToArray();
        }
#endif
    }
}
