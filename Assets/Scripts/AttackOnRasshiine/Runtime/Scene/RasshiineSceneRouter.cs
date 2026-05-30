using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class RasshiineSceneRouter : MonoBehaviour
    {
        private static RasshiineSceneRouter instance;

        [SerializeField] private RasshiineProductionScene currentScene = RasshiineProductionScene.Boot;
        [SerializeField] private RasshiineProductionScene loginFallbackScene = RasshiineProductionScene.Login;

        public static RasshiineSceneRouter Instance => instance;

        public RasshiineProductionScene CurrentScene => currentScene;

        public static RasshiineSceneRouter Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var existing = FindAnyObjectByType<RasshiineSceneRouter>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("Rasshiine Scene Router").AddComponent<RasshiineSceneRouter>();
        }

        public void SetCurrentScene(RasshiineProductionScene scene)
        {
            currentScene = scene;
        }

        public void LoadScene(RasshiineProductionScene scene)
        {
            currentScene = scene;
            SceneManager.LoadScene(RasshiineSceneCatalog.GetSceneName(scene), LoadSceneMode.Single);
        }

        public void ReturnToLogin()
        {
            LoadScene(loginFallbackScene);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
