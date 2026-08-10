using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public static class RasshiineUiLayoutQaRunner
    {
        private const string ReportPath = "/private/tmp/attack-on-rasshiine-ui-layout-qa.txt";

        [MenuItem("AttackOnRasshiine/Run UI Layout QA")]
        public static void RunUiLayoutQa()
        {
            try
            {
                var failures = RaidGameAppUiLayoutEditModeTests.RunAllPrimaryScreensLayoutQa()
                    .Concat(RaidGameAppUiLayoutEditModeTests.RunNonBattleInteractionQa())
                    .ToList();
                var report = failures.Count == 0
                    ? $"PASS {DateTime.Now:yyyy-MM-dd HH:mm:ss}: all primary UI screens fit at 6 tested resolutions; non-battle navigation and input checks passed."
                    : $"FAIL {DateTime.Now:yyyy-MM-dd HH:mm:ss}:{Environment.NewLine}{string.Join(Environment.NewLine, failures.Select(item => $"- {item}"))}";
                File.WriteAllText(ReportPath, report);

                if (failures.Count == 0)
                {
                    Debug.Log($"AttackOnRasshiine UI Layout QA passed. Report: {ReportPath}");
                }
                else
                {
                    Debug.LogError($"AttackOnRasshiine UI Layout QA found {failures.Count} issue(s). Report: {ReportPath}");
                }
            }
            catch (Exception ex)
            {
                var report = $"ERROR {DateTime.Now:yyyy-MM-dd HH:mm:ss}:{Environment.NewLine}{ex}";
                File.WriteAllText(ReportPath, report);
                Debug.LogError($"AttackOnRasshiine UI Layout QA crashed. Report: {ReportPath}{Environment.NewLine}{ex}");
            }
        }
    }
}
