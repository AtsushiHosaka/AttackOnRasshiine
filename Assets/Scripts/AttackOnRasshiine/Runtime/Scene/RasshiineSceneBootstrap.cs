using System;
using System.Collections;
using AttackOnRasshiine.Runtime.Services;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class RasshiineSceneBootstrap : MonoBehaviour
    {
        private const string SceneQueryKey = "scene";
        private const string FrontDisplaySceneSlug = "front-display";
        private const string FrontDisplayCompactSceneSlug = "frontdisplay";
        private const string BattleSceneSlug = "battle";

        [SerializeField] private RasshiineProductionScene sceneId = RasshiineProductionScene.Boot;
        [SerializeField] private RasshiineProductionScene bootNextScene = RasshiineProductionScene.Login;
        [SerializeField] private bool loadSupabaseConfigOnBoot = true;
        [SerializeField] private bool loadNextSceneOnBoot = true;

        public RasshiineProductionScene SceneId => sceneId;

        private IEnumerator Start()
        {
            var router = RasshiineSceneRouter.Ensure();
            router.SetCurrentScene(sceneId);

            if (sceneId != RasshiineProductionScene.Boot)
            {
                yield break;
            }

            if (loadSupabaseConfigOnBoot)
            {
                var supabase = new SupabaseGameClient();
                yield return supabase.LoadConfig();
            }

            if (!loadNextSceneOnBoot)
            {
                yield break;
            }

            yield return null;
            var nextScene = ResolveBootNextScene();
            if (nextScene == RasshiineProductionScene.Boot)
            {
                Debug.LogWarning("Rasshiine boot scene cannot route back to Boot.");
                yield break;
            }

            router.LoadScene(nextScene);
        }

        public static bool TryResolveSceneOverride(string absoluteUrl, out RasshiineProductionScene scene)
        {
            scene = RasshiineProductionScene.Login;
            if (string.IsNullOrWhiteSpace(absoluteUrl))
            {
                return false;
            }

            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            {
                return TryResolveSceneToken(absoluteUrl, out scene);
            }

            if (TryResolveSceneToken(GetQueryValue(uri.Query, SceneQueryKey), out scene))
            {
                return true;
            }

            var pathSegments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < pathSegments.Length; i++)
            {
                if (TryResolveSceneToken(Uri.UnescapeDataString(pathSegments[i]), out scene))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetQueryValue(string query, string key)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return string.Empty;
            }

            var trimmed = query.TrimStart('?');
            var pairs = trimmed.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i].Split(new[] { '=' }, 2);
                var candidateKey = Uri.UnescapeDataString(pair[0]).Trim();
                if (!string.Equals(candidateKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return pair.Length > 1 ? Uri.UnescapeDataString(pair[1]).Trim() : string.Empty;
            }

            return string.Empty;
        }

        private static bool TryResolveSceneToken(string value, out RasshiineProductionScene scene)
        {
            scene = RasshiineProductionScene.Login;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized == FrontDisplaySceneSlug || normalized == FrontDisplayCompactSceneSlug)
            {
                scene = RasshiineProductionScene.FrontDisplay;
                return true;
            }

            if (normalized == BattleSceneSlug)
            {
                scene = RasshiineProductionScene.Battle;
                return true;
            }

            return false;
        }

        private RasshiineProductionScene ResolveBootNextScene()
        {
            return TryResolveSceneOverride(Application.absoluteURL, out var scene)
                ? scene
                : bootNextScene;
        }
    }
}
