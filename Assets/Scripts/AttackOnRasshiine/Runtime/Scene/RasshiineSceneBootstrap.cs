using System.Collections;
using AttackOnRasshiine.Runtime.Services;
using UnityEngine;
#if UNITY_WEBGL && DEVELOPMENT_BUILD && !UNITY_EDITOR
using AttackOnRasshiine.Runtime.QA;
#endif

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class RasshiineSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private RasshiineProductionScene sceneId = RasshiineProductionScene.Boot;
        [SerializeField] private RasshiineProductionScene bootNextScene = RasshiineProductionScene.Login;
        [SerializeField] private bool loadSupabaseConfigOnBoot = true;
        [SerializeField] private bool loadNextSceneOnBoot = true;

        public RasshiineProductionScene SceneId => sceneId;

        // Called by the WebGL host through UnityInstance.SendMessage after the player boots.
        // Only RasshiineWebRouteIntent's allow-listed route names can affect scene loading.
        public void ApplyWebRoute(string route)
        {
            if (!RasshiineWebRouteIntent.TrySet(route))
            {
                Debug.LogWarning("Rejected an unknown AttackOnRasshiine web route.");
                return;
            }

            ApplyPendingWebRoute();
        }

        private IEnumerator Start()
        {
            var router = RasshiineSceneRouter.Ensure();
            router.SetCurrentScene(sceneId);

            if (sceneId != RasshiineProductionScene.Boot)
            {
                yield break;
            }

#if UNITY_WEBGL && DEVELOPMENT_BUILD && !UNITY_EDITOR
            // The visual-QA entry point exists only in a Development WebGL
            // compilation and accepts only an explicit loopback URL. Resolve it
            // before loading runtime configuration so a QA artifact never sends
            // credentials or requests to the production backend.
            if (WebGlVisualQaContext.TryActivate(Application.absoluteURL, out var qaIntent, out var qaRejection))
            {
                router.LoadScene(qaIntent.Scene);
                yield break;
            }

            if (Application.absoluteURL.IndexOf("aor-visual-qa", System.StringComparison.Ordinal) >= 0)
            {
                Debug.LogError($"Rejected local WebGL visual-QA request: {qaRejection}");
            }
#endif

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
            if (bootNextScene == RasshiineProductionScene.Boot)
            {
                Debug.LogWarning("Rasshiine boot scene cannot route back to Boot.");
                yield break;
            }

            router.LoadScene(bootNextScene);
        }

        private void ApplyPendingWebRoute()
        {
            if (!RasshiineWebRouteIntent.TryPeek(out var requestedScene))
            {
                return;
            }

            var router = RasshiineSceneRouter.Ensure();
            if (requestedScene == RasshiineProductionScene.FrontDisplay)
            {
                RasshiineWebRouteIntent.Consume(requestedScene);
                // The browser-facing front display is a public route. Drop every
                // authenticated in-memory reference before entering it and reload even
                // when already there so an existing scene instance cannot retain a
                // private snapshot or bearer token.
                PreparePublicFrontDisplaySession();
                router.LoadScene(requestedScene);
                return;
            }

            if (requestedScene != RasshiineProductionScene.Battle)
            {
                return;
            }

            if (RasshiineRuntimeSession.CurrentUser != null)
            {
                RasshiineWebRouteIntent.Consume(requestedScene);
                if (sceneId != requestedScene)
                {
                    router.LoadScene(requestedScene);
                }

                return;
            }

            // Battle requires authentication. Keep the intent until login completes, but
            // never leave an unauthenticated browser sitting in an authenticated scene.
            if (sceneId != RasshiineProductionScene.Boot && sceneId != RasshiineProductionScene.Login)
            {
                router.ReturnToLogin();
            }
        }

        private static void PreparePublicFrontDisplaySession()
        {
            RasshiineRuntimeSession.Clear();
        }
    }
}
