using UnityEngine;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class RasshiineTheme : MonoBehaviour
    {
        public const string HeatButtonPrefabPath = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Button/Button.prefab";
        public const string HeatInputFieldPrefabPath = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Input Field/Input Field.prefab";
        public const string HeatProgressBarPrefabPath = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Progress Bar/Progress Bar.prefab";
        public const string TinyHeroMemberPrefabPath = "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC01.prefab";

        [Header("Sprites")]
        public bool UseHeatUiSkin;
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

        [Header("Heat UI Prefabs")]
        public GameObject HeatButtonPrefab;
        public GameObject HeatInputFieldPrefab;
        public GameObject HeatProgressBarPrefab;

        public readonly Color Void = new(0.015f, 0.027f, 0.09f, 1f);
        public readonly Color Panel = new(0.04f, 0.09f, 0.25f, 0.92f);
        public readonly Color Cyan = new(0.09f, 0.84f, 1f, 1f);
        public readonly Color Magenta = new(1f, 0.22f, 0.85f, 1f);
        public readonly Color Purple = new(0.49f, 0.27f, 1f, 1f);
        public readonly Color Mint = new(0.13f, 1f, 0.78f, 1f);
        public readonly Color Gold = new(1f, 0.85f, 0.29f, 1f);
        public readonly Color Text = new(0.96f, 0.98f, 1f, 1f);
        public readonly Color MutedText = new(0.63f, 0.72f, 0.9f, 1f);

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
        }
#endif
    }
}
