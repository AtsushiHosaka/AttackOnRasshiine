using System.Collections;
using AttackOnRasshiine.Runtime.Services;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class RasshiineSceneBootstrap : MonoBehaviour
    {
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

            var normalized = absoluteUrl.ToLowerInvariant();
            if (normalized.Contains("scene=front-display") || normalized.Contains("scene=frontdisplay") || normalized.Contains("/front-display"))
            {
                scene = RasshiineProductionScene.FrontDisplay;
                return true;
            }

            if (normalized.Contains("scene=battle") || normalized.Contains("/battle"))
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
