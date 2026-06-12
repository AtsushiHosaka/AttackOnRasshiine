using AttackOnRasshiine.Runtime.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public static class RasshiineUiPreviewLauncher
    {
        private const string EditorPreviewEnabledKey = "AttackOnRasshiine.EditorPreview.Enabled";
        private const string EditorPreviewLoginIdKey = "AttackOnRasshiine.EditorPreview.LoginId";

        [MenuItem("AttackOnRasshiine/Preview Mentor Dashboard")]
        public static void PreviewMentorDashboard()
        {
            PreviewAs("mentor1", RasshiineProductionScene.MentorDashboard);
        }

        [MenuItem("AttackOnRasshiine/Preview Login")]
        public static void PreviewLogin()
        {
            EditorPrefs.SetBool(EditorPreviewEnabledKey, false);
            EditorSceneManager.OpenScene(RasshiineSceneCatalog.GetScenePath(RasshiineProductionScene.Login));
            EditorApplication.isPlaying = true;
        }

        private static void PreviewAs(string loginId, RasshiineProductionScene scene)
        {
            EditorPrefs.SetString(EditorPreviewLoginIdKey, loginId);
            EditorPrefs.SetBool(EditorPreviewEnabledKey, true);
            EditorSceneManager.OpenScene(RasshiineSceneCatalog.GetScenePath(scene));
            EditorApplication.isPlaying = true;
        }
    }
}
