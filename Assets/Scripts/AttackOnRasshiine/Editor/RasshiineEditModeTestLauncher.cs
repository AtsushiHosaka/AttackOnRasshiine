using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    /// <summary>
    /// Provides a deterministic menu entry for running the complete EditMode suite
    /// while the project is already open in the Unity Editor.
    /// </summary>
    public static class RasshiineEditModeTestLauncher
    {
        private const string ReportPath = "/private/tmp/attack-on-rasshiine-editmode-tests.txt";
        private static TestRunnerApi testRunnerApi;
        private static TestRunCallbacks callbacks;

        [MenuItem("AttackOnRasshiine/Run All EditMode Tests")]
        public static void RunAllEditModeTests()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunAllEditModeTests;
                return;
            }

            File.WriteAllText(ReportPath, $"RUNNING {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new TestRunCallbacks();
            testRunnerApi.RegisterCallbacks(callbacks);
            testRunnerApi.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode
            }));

            Debug.Log($"AttackOnRasshiine EditMode test run started. Report: {ReportPath}");
        }

        private sealed class TestRunCallbacks : ICallbacks
        {
            private readonly List<string> failures = new();

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                var status = result.FailCount == 0 && result.InconclusiveCount == 0 ? "PASS" : "FAIL";
                var reportBuilder = new StringBuilder();
                reportBuilder.AppendLine(
                    $"{status} {DateTime.Now:yyyy-MM-dd HH:mm:ss}: total={total}, passed={result.PassCount}, " +
                    $"failed={result.FailCount}, skipped={result.SkipCount}, inconclusive={result.InconclusiveCount}");
                if (failures.Count > 0)
                {
                    reportBuilder.AppendLine("Failures:");
                    foreach (var failure in failures)
                    {
                        reportBuilder.AppendLine(failure);
                    }
                }
                var report = reportBuilder.ToString();

                File.WriteAllText(ReportPath, report);
                Debug.Log($"AttackOnRasshiine EditMode tests finished: {report.Trim()}");

                testRunnerApi?.UnregisterCallbacks(this);
                if (testRunnerApi != null)
                {
                    UnityEngine.Object.DestroyImmediate(testRunnerApi);
                }

                testRunnerApi = null;
                callbacks = null;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.FailCount <= 0 || result.HasChildren)
                {
                    return;
                }

                failures.Add($"- {result.FullName}\n  {result.Message}\n  {result.StackTrace}");
            }
        }
    }
}
