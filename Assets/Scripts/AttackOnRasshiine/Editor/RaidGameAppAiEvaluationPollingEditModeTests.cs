using System.Reflection;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidGameAppAiEvaluationPollingEditModeTests
    {
        [Test]
        public void AiEvaluationSnapshotPollingUsesFiniteTwoFourEightSecondBackoff()
        {
            Assert.AreEqual(2f, InvokeStatic<float>("AiEvaluationSnapshotPollDelaySeconds", 0));
            Assert.AreEqual(4f, InvokeStatic<float>("AiEvaluationSnapshotPollDelaySeconds", 1));
            Assert.AreEqual(8f, InvokeStatic<float>("AiEvaluationSnapshotPollDelaySeconds", 2));
            Assert.AreEqual(8f, InvokeStatic<float>("AiEvaluationSnapshotPollDelaySeconds", 99), "Attempts beyond the finite budget must not create an unbounded delay schedule.");
        }

        [Test]
        public void OnlyUnevaluatedAiPendingSessionsNeedSnapshotPolling()
        {
            var pending = new DevSession { Status = DevSessionStatus.AiPending };
            Assert.IsTrue(InvokeStatic<bool>("ShouldPollAiEvaluationSnapshot", pending));

            pending.Evaluation = new AiEvaluation { Rank = AiRank.A };
            Assert.IsFalse(InvokeStatic<bool>("ShouldPollAiEvaluationSnapshot", pending));
            Assert.IsFalse(InvokeStatic<bool>("ShouldPollAiEvaluationSnapshot", new DevSession { Status = DevSessionStatus.Pending }));
            // Cast null to a single params element.  A bare null is otherwise
            // bound as the entire object[] and reflection receives zero arguments.
            Assert.IsFalse(InvokeStatic<bool>("ShouldPollAiEvaluationSnapshot", (object)null));
        }

        [Test]
        public void LeavingDevLogCancelsImmediatelyButSameScreenRerenderKeepsPollingGeneration()
        {
            var host = new GameObject("RaidGameAppAiPollingTests");
            try
            {
                var app = host.AddComponent<RaidGameApp>();
                SetField(app, "activeProductionScene", RasshiineProductionScene.DevLog);
                SetField(app, "aiEvaluationSnapshotPollGeneration", 41);

                Invoke(app, "MarkScene", RasshiineProductionScene.DevLog);
                Assert.AreEqual(41, GetField(app, "aiEvaluationSnapshotPollGeneration"), "A DevLog rerender must not cancel its in-flight completion refresh.");

                Invoke(app, "MarkScene", RasshiineProductionScene.MemberHome);
                Assert.AreEqual(42, GetField(app, "aiEvaluationSnapshotPollGeneration"), "Leaving DevLog must invalidate the polling token immediately.");

                Invoke(app, "ClearAuthenticatedUiState");
                Assert.AreEqual(43, GetField(app, "aiEvaluationSnapshotPollGeneration"), "Logout/session-expiry cleanup must invalidate any remaining poll.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static T InvokeStatic<T>(string methodName, params object[] arguments)
        {
            var method = typeof(RaidGameApp).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            return (T)method.Invoke(null, arguments);
        }

        private static void Invoke(RaidGameApp app, string methodName, params object[] arguments)
        {
            var method = typeof(RaidGameApp).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(app, arguments);
        }

        private static void SetField(RaidGameApp app, string fieldName, object value)
        {
            var field = typeof(RaidGameApp).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(app, value);
        }

        private static object GetField(RaidGameApp app, string fieldName)
        {
            var field = typeof(RaidGameApp).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field.GetValue(app);
        }
    }
}
