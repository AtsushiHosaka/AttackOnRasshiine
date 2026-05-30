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
            router.LoadScene(bootNextScene);
        }
    }
}
