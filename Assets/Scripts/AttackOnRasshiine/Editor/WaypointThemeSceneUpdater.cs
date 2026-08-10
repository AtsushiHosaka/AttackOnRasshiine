using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public static class WaypointThemeSceneUpdater
    {
        [MenuItem("AttackOnRasshiine/Apply Waypoint Terrace UI Theme")]
        public static void ApplyToProductionScenes()
        {
            ImportUiAssets();
            var originalScenePath = SceneManager.GetActiveScene().path;
            var paths = new List<string>(RasshiineSceneCatalog.GetProductionScenePaths())
            {
                RasshiineSceneCatalog.PrototypeScenePath,
                RasshiineSceneCatalog.LegacyProductionScenePath
            };

            foreach (var path in paths)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (sceneAsset == null)
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var theme in root.GetComponentsInChildren<RasshiineTheme>(true))
                    {
                        AssignUiTheme(theme);
                        EditorUtility.SetDirty(theme);
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(originalScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScenePath) != null)
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            Debug.Log("Applied Waypoint Terrace UI assets and fonts without rebuilding environment scenes.");
        }

        private static void ImportUiAssets()
        {
            foreach (var path in new[]
                     {
                         RasshiineTheme.UiFontPath,
                         RasshiineTheme.UiTitleFontPath,
                         RasshiineTheme.UiDisplayFontPath,
                         RasshiineTheme.WaypointCompassPath,
                         RasshiineTheme.WaypointCrestPath,
                         RasshiineTheme.WaypointNavRingPath,
                         RasshiineTheme.WaypointNextRaidFramePath,
                         RasshiineTheme.WaypointParchmentPath,
                         RasshiineTheme.WaypointPlayerStatusFramePath
                     })
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void AssignUiTheme(RasshiineTheme theme)
        {
            theme.UseHeatUiSkin = true;
            theme.UiFont = AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiFontPath);
            theme.UiTitleFont = AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiTitleFontPath);
            theme.UiDisplayFont = AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiDisplayFontPath);
            theme.WaypointCompass = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointCompassPath);
            theme.WaypointCrest = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointCrestPath);
            theme.WaypointNavRing = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointNavRingPath);
            theme.WaypointNextRaidFrame = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointNextRaidFramePath);
            theme.WaypointParchment = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointParchmentPath);
            theme.WaypointPlayerStatusFrame = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointPlayerStatusFramePath);
            theme.TeamIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Multiplayer (64x).png");
            theme.GoalIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Achievements (64x).png");
            theme.RecordIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Chapters (64x).png");
        }
    }
}
