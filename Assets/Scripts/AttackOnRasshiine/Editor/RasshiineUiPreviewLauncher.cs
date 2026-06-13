using AttackOnRasshiine.Runtime.Scene;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    [InitializeOnLoad]
    public static class RasshiineUiPreviewLauncher
    {
        private const string EditorPreviewEnabledKey = "AttackOnRasshiine.EditorPreview.Enabled";
        private const string EditorPreviewLoginIdKey = "AttackOnRasshiine.EditorPreview.LoginId";
        private const string EditorPreviewQueuedKey = "AttackOnRasshiine.EditorPreview.Queued";
        private const string EditorPreviewSceneKey = "AttackOnRasshiine.EditorPreview.Scene";
        private const string EditorPreviewBattleSetupKey = "AttackOnRasshiine.EditorPreview.BattleSetup";

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
            QueuePreview("member1", RasshiineProductionScene.Battle, true, "scheduled");
        }

        [MenuItem("AttackOnRasshiine/Preview Battle Active")]
        public static void PreviewBattleActive()
        {
            QueuePreview("member1", RasshiineProductionScene.Battle, true, "active");
        }

        [MenuItem("AttackOnRasshiine/Preview Battle Coop Turn")]
        public static void PreviewBattleCoopTurn()
        {
            QueuePreview("member1", RasshiineProductionScene.Battle, true, "coop-turn");
        }

        [MenuItem("AttackOnRasshiine/Preview Battle Result")]
        public static void PreviewBattleResult()
        {
            QueuePreview("member1", RasshiineProductionScene.Battle, true, "result");
        }

        [MenuItem("AttackOnRasshiine/Preview Front Display")]
        public static void PreviewFrontDisplay()
        {
            QueuePreview(string.Empty, RasshiineProductionScene.FrontDisplay, false);
        }

        [MenuItem("AttackOnRasshiine/Preview Front Display Active")]
        public static void PreviewFrontDisplayActive()
        {
            QueuePreview(string.Empty, RasshiineProductionScene.FrontDisplay, false, "coop-turn");
        }

        [MenuItem("AttackOnRasshiine/Capture Game Screenshot")]
        public static void CaptureGameScreenshot()
        {
            var filename = $"aor-game-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
            var path = Path.Combine(Path.GetTempPath(), filename);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"AttackOnRasshiine game screenshot saved: {path}");
        }

        private static void QueuePreview(string loginId, RasshiineProductionScene scene, bool authenticated, string battleSetup = "")
        {
            EditorPrefs.SetString(EditorPreviewLoginIdKey, loginId);
            EditorPrefs.SetBool(EditorPreviewEnabledKey, authenticated);
            EditorPrefs.SetString(EditorPreviewSceneKey, scene.ToString());
            EditorPrefs.SetString(EditorPreviewBattleSetupKey, battleSetup ?? string.Empty);
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
