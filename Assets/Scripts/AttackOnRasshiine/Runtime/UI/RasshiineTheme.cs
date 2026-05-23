using UnityEngine;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class RasshiineTheme : MonoBehaviour
    {
        [Header("Sprites")]
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

        [Header("Materials")]
        public Material SkyboxMaterial;
        public Material BossMaterial;
        public Material MemberMaterial;
        public Material FloorLineMaterial;
        public Material ProjectileMaterial;

        [Header("Model placeholders")]
        public GameObject MentorPlaceholderPrefab;
        public GameObject MemberPlaceholderPrefab;

        public readonly Color Void = new(0.015f, 0.027f, 0.09f, 1f);
        public readonly Color Panel = new(0.04f, 0.09f, 0.25f, 0.92f);
        public readonly Color Cyan = new(0.09f, 0.84f, 1f, 1f);
        public readonly Color Magenta = new(1f, 0.22f, 0.85f, 1f);
        public readonly Color Purple = new(0.49f, 0.27f, 1f, 1f);
        public readonly Color Mint = new(0.13f, 1f, 0.78f, 1f);
        public readonly Color Gold = new(1f, 0.85f, 0.29f, 1f);
        public readonly Color Text = new(0.96f, 0.98f, 1f, 1f);
        public readonly Color MutedText = new(0.63f, 0.72f, 0.9f, 1f);
    }
}
