using System;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Scene
{
    public enum RasshiineProductionScene
    {
        Boot,
        Login,
        MemberHome,
        DevLog,
        MentorDashboard,
        Battle,
        FrontDisplay
    }

    public sealed class RasshiineProductionSceneDefinition
    {
        public RasshiineProductionSceneDefinition(
            RasshiineProductionScene scene,
            string sceneName,
            string scenePath,
            bool requiresLogin,
            bool isReadOnly,
            float pollingIntervalSeconds)
        {
            Scene = scene;
            SceneName = sceneName;
            ScenePath = scenePath;
            RequiresLogin = requiresLogin;
            IsReadOnly = isReadOnly;
            PollingIntervalSeconds = pollingIntervalSeconds;
        }

        public RasshiineProductionScene Scene { get; }
        public string SceneName { get; }
        public string ScenePath { get; }
        public bool RequiresLogin { get; }
        public bool IsReadOnly { get; }
        public float PollingIntervalSeconds { get; }
    }

    public static class RasshiineSceneCatalog
    {
        public const string SceneAssetDirectory = "Assets/Scenes";
        public const string PrototypeScenePath = SceneAssetDirectory + "/RasshiineRaidPrototype.unity";
        public const string LegacyProductionScenePath = SceneAssetDirectory + "/RasshiineProduction.unity";

        private static readonly RasshiineProductionScene[] ProductionBuildOrderValue =
        {
            RasshiineProductionScene.Boot,
            RasshiineProductionScene.Login,
            RasshiineProductionScene.MemberHome,
            RasshiineProductionScene.DevLog,
            RasshiineProductionScene.MentorDashboard,
            RasshiineProductionScene.Battle,
            RasshiineProductionScene.FrontDisplay
        };

        private static readonly RasshiineProductionSceneDefinition[] ProductionSceneDefinitionsValue =
        {
            new(RasshiineProductionScene.Boot, "RasshiineBoot", GetSceneAssetPath("RasshiineBoot"), false, true, 0f),
            new(RasshiineProductionScene.Login, "RasshiineLogin", GetSceneAssetPath("RasshiineLogin"), false, false, 0f),
            new(RasshiineProductionScene.MemberHome, "RasshiineMemberHome", GetSceneAssetPath("RasshiineMemberHome"), true, false, 0f),
            new(RasshiineProductionScene.DevLog, "RasshiineDevLog", GetSceneAssetPath("RasshiineDevLog"), true, false, 0f),
            new(RasshiineProductionScene.MentorDashboard, "RasshiineMentorDashboard", GetSceneAssetPath("RasshiineMentorDashboard"), true, false, 0f),
            new(RasshiineProductionScene.Battle, "RasshiineBattle", GetSceneAssetPath("RasshiineBattle"), true, false, 4f),
            new(RasshiineProductionScene.FrontDisplay, "RasshiineFrontDisplay", GetSceneAssetPath("RasshiineFrontDisplay"), false, true, 4f)
        };

        public static IReadOnlyList<RasshiineProductionScene> ProductionBuildOrder => ProductionBuildOrderValue;
        public static IReadOnlyList<RasshiineProductionSceneDefinition> ProductionSceneDefinitions => ProductionSceneDefinitionsValue;

        public static string GetSceneName(RasshiineProductionScene scene)
        {
            return Get(scene).SceneName;
        }

        public static string GetScenePath(RasshiineProductionScene scene)
        {
            return Get(scene).ScenePath;
        }

        public static RasshiineProductionSceneDefinition Get(RasshiineProductionScene scene)
        {
            for (var i = 0; i < ProductionSceneDefinitionsValue.Length; i++)
            {
                var definition = ProductionSceneDefinitionsValue[i];
                if (definition.Scene == scene)
                {
                    return definition;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(scene), scene, null);
        }

        public static string[] GetProductionScenePaths()
        {
            var paths = new string[ProductionBuildOrderValue.Length];
            for (var i = 0; i < ProductionBuildOrderValue.Length; i++)
            {
                paths[i] = GetScenePath(ProductionBuildOrderValue[i]);
            }

            return paths;
        }

        public static bool IsProductionScenePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            for (var i = 0; i < ProductionBuildOrderValue.Length; i++)
            {
                if (string.Equals(path, GetScenePath(ProductionBuildOrderValue[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsDisplayOnlyScene(RasshiineProductionScene scene)
        {
            return scene == RasshiineProductionScene.FrontDisplay;
        }

        public static bool RequiresAuthenticatedUser(RasshiineProductionScene scene)
        {
            return scene != RasshiineProductionScene.Boot
                && scene != RasshiineProductionScene.Login
                && !IsDisplayOnlyScene(scene);
        }

        public static RasshiineProductionScene GetAuthenticatedHomeScene(UserRole role)
        {
            return role == UserRole.Mentor
                ? RasshiineProductionScene.MentorDashboard
                : RasshiineProductionScene.MemberHome;
        }

        public static bool TryGetSceneByName(string sceneName, out RasshiineProductionScene scene)
        {
            for (var i = 0; i < ProductionBuildOrderValue.Length; i++)
            {
                var candidate = ProductionBuildOrderValue[i];
                if (string.Equals(sceneName, GetSceneName(candidate), StringComparison.Ordinal))
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = RasshiineProductionScene.Login;
            return false;
        }

        private static string GetSceneAssetPath(string sceneName)
        {
            return $"{SceneAssetDirectory}/{sceneName}.unity";
        }
    }
}
