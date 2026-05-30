using System;
using System.Collections.Generic;

namespace AttackOnRasshiine.Runtime.Scene
{
    public enum ProductionSceneKind
    {
        Login,
        Battle,
        FrontDisplay,
        MentorDashboard
    }

    public sealed class ProductionSceneDefinition
    {
        public ProductionSceneDefinition(
            ProductionSceneKind kind,
            string sceneName,
            string scenePath,
            bool requiresLogin,
            bool isReadOnly,
            float pollingIntervalSeconds)
        {
            Kind = kind;
            SceneName = sceneName;
            ScenePath = scenePath;
            RequiresLogin = requiresLogin;
            IsReadOnly = isReadOnly;
            PollingIntervalSeconds = pollingIntervalSeconds;
        }

        public ProductionSceneKind Kind { get; }
        public string SceneName { get; }
        public string ScenePath { get; }
        public bool RequiresLogin { get; }
        public bool IsReadOnly { get; }
        public float PollingIntervalSeconds { get; }
    }

    public static class ProductionSceneCatalog
    {
        public const string LoginScenePath = "Assets/Scenes/Login.unity";
        public const string BattleScenePath = "Assets/Scenes/Battle.unity";
        public const string FrontDisplayScenePath = "Assets/Scenes/FrontDisplay.unity";
        public const string MentorDashboardScenePath = "Assets/Scenes/MentorDashboard.unity";

        public static readonly IReadOnlyList<ProductionSceneDefinition> BuildSettingsScenes = new[]
        {
            new ProductionSceneDefinition(ProductionSceneKind.Login, "Login", LoginScenePath, false, false, 0f),
            new ProductionSceneDefinition(ProductionSceneKind.Battle, "Battle", BattleScenePath, true, false, 4f),
            new ProductionSceneDefinition(ProductionSceneKind.FrontDisplay, "FrontDisplay", FrontDisplayScenePath, false, true, 4f),
            new ProductionSceneDefinition(ProductionSceneKind.MentorDashboard, "MentorDashboard", MentorDashboardScenePath, true, false, 8f)
        };

        public static ProductionSceneDefinition Get(ProductionSceneKind kind)
        {
            foreach (var scene in BuildSettingsScenes)
            {
                if (scene.Kind == kind)
                {
                    return scene;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown production scene kind.");
        }
    }
}
