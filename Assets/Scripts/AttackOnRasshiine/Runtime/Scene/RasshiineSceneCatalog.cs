using System;
using System.Collections.Generic;

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

        public static IReadOnlyList<RasshiineProductionScene> ProductionBuildOrder => ProductionBuildOrderValue;

        public static string GetSceneName(RasshiineProductionScene scene)
        {
            return scene switch
            {
                RasshiineProductionScene.Boot => "RasshiineBoot",
                RasshiineProductionScene.Login => "RasshiineLogin",
                RasshiineProductionScene.MemberHome => "RasshiineMemberHome",
                RasshiineProductionScene.DevLog => "RasshiineDevLog",
                RasshiineProductionScene.MentorDashboard => "RasshiineMentorDashboard",
                RasshiineProductionScene.Battle => "RasshiineBattle",
                RasshiineProductionScene.FrontDisplay => "RasshiineFrontDisplay",
                _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
            };
        }

        public static string GetScenePath(RasshiineProductionScene scene)
        {
            return $"{SceneAssetDirectory}/{GetSceneName(scene)}.unity";
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
    }
}
