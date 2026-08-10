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
        private const string EditorPreviewQueuedAtKey = "AttackOnRasshiine.EditorPreview.QueuedAt";
        private const string EditorPreviewSceneKey = "AttackOnRasshiine.EditorPreview.Scene";
        private const string EditorPreviewBattleSetupKey = "AttackOnRasshiine.EditorPreview.BattleSetup";
        private const string EditorPreviewScreenKey = "AttackOnRasshiine.EditorPreview.Screen";
        private const double MinRelaunchDelaySeconds = 0.65d;

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

        [MenuItem("AttackOnRasshiine/Preview Member Products")]
        public static void PreviewMemberProducts()
        {
            QueuePreview("member1", RasshiineProductionScene.MemberHome, true, string.Empty, "products");
        }

        [MenuItem("AttackOnRasshiine/Preview Member Achievements")]
        public static void PreviewMemberAchievements()
        {
            QueuePreview("member1", RasshiineProductionScene.MemberHome, true, string.Empty, "achievements");
        }

        [MenuItem("AttackOnRasshiine/Preview Ranking")]
        public static void PreviewRanking()
        {
            QueuePreview("member1", RasshiineProductionScene.MemberHome, true, string.Empty, "ranking");
        }

        [MenuItem("AttackOnRasshiine/Preview Member Settings")]
        public static void PreviewMemberSettings()
        {
            QueuePreview("member1", RasshiineProductionScene.MemberHome, true, string.Empty, "settings");
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

        [MenuItem("AttackOnRasshiine/Preview Mentor Operations")]
        public static void PreviewMentorOperations()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true, string.Empty, "mentor-operations");
        }

        [MenuItem("AttackOnRasshiine/Preview Mentor Review Queue")]
        public static void PreviewMentorReviewQueue()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true, string.Empty, "mentor-review");
        }

        [MenuItem("AttackOnRasshiine/Preview Mentor Team Status")]
        public static void PreviewMentorTeamStatus()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true, string.Empty, "mentor-team");
        }

        [MenuItem("AttackOnRasshiine/Preview Mentor Accounts")]
        public static void PreviewMentorAccounts()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true, string.Empty, "mentor-accounts");
        }

        [MenuItem("AttackOnRasshiine/Preview Mentor Products")]
        public static void PreviewMentorProducts()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true, string.Empty, "products");
        }

        [MenuItem("AttackOnRasshiine/Preview Mentor Achievements")]
        public static void PreviewMentorAchievements()
        {
            QueuePreview("mentor1", RasshiineProductionScene.MentorDashboard, true, string.Empty, "achievements");
        }

        [MenuItem("AttackOnRasshiine/Capture Game Screenshot")]
        public static void CaptureGameScreenshot()
        {
            var filename = $"aor-game-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
            var path = Path.Combine(Path.GetTempPath(), filename);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"AttackOnRasshiine game screenshot saved: {path}");
        }

        [MenuItem("AttackOnRasshiine/Maximize Game View")]
        public static void MaximizeGameView()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogError("Unity Game View type could not be resolved.");
                return;
            }

            var gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show();
            gameView.Focus();
            gameView.maximized = true;
        }

        private static void QueuePreview(string loginId, RasshiineProductionScene scene, bool authenticated, string battleSetup = "", string screen = "")
        {
            EditorPrefs.SetString(EditorPreviewLoginIdKey, loginId);
            EditorPrefs.SetBool(EditorPreviewEnabledKey, authenticated);
            EditorPrefs.SetString(EditorPreviewSceneKey, scene.ToString());
            EditorPrefs.SetString(EditorPreviewBattleSetupKey, battleSetup ?? string.Empty);
            EditorPrefs.SetString(EditorPreviewScreenKey, screen ?? string.Empty);
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorPrefs.SetBool(EditorPreviewQueuedKey, true);
                EditorPrefs.SetString(EditorPreviewQueuedAtKey, EditorApplication.timeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

            if (EditorApplication.timeSinceStartup - QueuedAtSeconds() < MinRelaunchDelaySeconds)
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

        private static double QueuedAtSeconds()
        {
            return double.TryParse(
                EditorPrefs.GetString(EditorPreviewQueuedAtKey, "0"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0d;
        }

        private static void LaunchPreview(RasshiineProductionScene scene)
        {
            EditorSceneManager.OpenScene(RasshiineSceneCatalog.GetScenePath(scene));
            EditorApplication.isPlaying = true;
        }
    }
}
