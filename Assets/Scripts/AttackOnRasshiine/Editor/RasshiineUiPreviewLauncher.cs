using AttackOnRasshiine.Runtime.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    [InitializeOnLoad]
    public static class RasshiineUiPreviewLauncher
    {
        private const string EditorPreviewEnabledKey = "AttackOnRasshiine.EditorPreview.Enabled";
        private const string EditorPreviewLoginIdKey = "AttackOnRasshiine.EditorPreview.LoginId";
        private const string EditorPreviewQueuedKey = "AttackOnRasshiine.EditorPreview.Queued";
        private const string EditorPreviewSceneKey = "AttackOnRasshiine.EditorPreview.Scene";

        static RasshiineUiPreviewLauncher()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= TryLaunchQueuedPreview;
            EditorApplication.update += TryLaunchQueuedPreview;
        }

        [MenuItem("AttackOnRasshiine/Preview Mentor Dashboard")]
        public static void PreviewMentorDashboard()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true);
        }

        [MenuItem("AttackOnRasshiine/Preview Login")]
        public static void PreviewLogin()
        {
            QueuePreview(string.Empty, RasshiineProductionScene.Login, false);
        }

        [MenuItem("AttackOnRasshiine/Preview Member Home")]
        public static void PreviewMemberHome()
        {
            QueuePreview("member1", RasshiineProductionScene.MemberHome, true);
        }

        [MenuItem("AttackOnRasshiine/Preview Dev Log")]
        public static void PreviewDevLog()
        {
            QueuePreview("member1", RasshiineProductionScene.DevLog, true);
        }

        [MenuItem("AttackOnRasshiine/Preview Battle")]
        public static void PreviewBattle()
        {
            QueuePreview("member1", RasshiineProductionScene.Battle, true);
        }

        [MenuItem("AttackOnRasshiine/Preview Front Display")]
        public static void PreviewFrontDisplay()
        {
            QueuePreview(string.Empty, RasshiineProductionScene.FrontDisplay, false);
        }

        private static void QueuePreview(string loginId, RasshiineProductionScene scene, bool authenticated)
        {
            EditorPrefs.SetString(EditorPreviewLoginIdKey, loginId);
            EditorPrefs.SetBool(EditorPreviewEnabledKey, authenticated);
            EditorPrefs.SetString(EditorPreviewSceneKey, scene.ToString());
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorPrefs.SetBool(EditorPreviewQueuedKey, true);
                EditorApplication.isPlaying = false;
                return;
            }

            LaunchPreview(scene);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                TryLaunchQueuedPreview();
            }
        }

        private static void TryLaunchQueuedPreview()
        {
            if (!EditorPrefs.GetBool(EditorPreviewQueuedKey, false)
                || EditorApplication.isPlaying
                || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
            {
                return;
            }

            EditorPrefs.SetBool(EditorPreviewQueuedKey, false);
            if (!System.Enum.TryParse(EditorPrefs.GetString(EditorPreviewSceneKey, RasshiineProductionScene.Login.ToString()), out RasshiineProductionScene scene))
            {
                scene = RasshiineProductionScene.Login;
            }

            LaunchPreview(scene);
        }

        private static void LaunchPreview(RasshiineProductionScene scene)
        {
            EditorSceneManager.OpenScene(RasshiineSceneCatalog.GetScenePath(scene));
            EditorApplication.isPlaying = true;
        }
    }
}
