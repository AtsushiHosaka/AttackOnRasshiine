using System.Reflection;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidGameAppSessionPrivacyEditModeTests
    {
        [Test]
        public void LogoutBoundaryClearsDraftsAndTemporaryPasswordFeedback()
        {
            var host = new GameObject("RaidGameAppSessionPrivacyTests");
            try
            {
                var app = host.AddComponent<RaidGameApp>();
                SetField(app, "retainedLoginId", "private-login-id");
                SetField(app, "retainedGoalDraft", "private goal");
                SetField(app, "retainedReflectionDraft", "private reflection");
                SetField(app, "retainedProductUrlDraft", "https://private.example/product");
                SetField(app, "retainedAchievementDescriptionDraft", "private achievement");
                SetField(app, "retainedMemberLoginIdDraft", "new-private-account");
                SetField(app, "lastMentorMessage", "初回パスワード: AOR-secret-value");
                SetField(app, "lastSessionMessage", "private session feedback");
                SetField(app, "pendingTemporaryPasswordReveal", "AOR-secret-value");
                SetField(app, "pendingTemporaryPasswordNickname", "private nickname");
                SetField(app, "temporaryPasswordRevealVisible", true);

                Invoke(app, "ClearAuthenticatedUiState");

                Assert.AreEqual(string.Empty, GetField(app, "retainedLoginId"));
                Assert.AreEqual(string.Empty, GetField(app, "retainedGoalDraft"));
                Assert.AreEqual(string.Empty, GetField(app, "retainedReflectionDraft"));
                Assert.AreEqual(string.Empty, GetField(app, "retainedProductUrlDraft"));
                Assert.AreEqual(string.Empty, GetField(app, "retainedAchievementDescriptionDraft"));
                Assert.AreEqual(string.Empty, GetField(app, "retainedMemberLoginIdDraft"));
                Assert.AreEqual(string.Empty, GetField(app, "lastMentorMessage"));
                Assert.AreEqual(string.Empty, GetField(app, "lastSessionMessage"));
                Assert.AreEqual(string.Empty, GetField(app, "pendingTemporaryPasswordReveal"));
                Assert.AreEqual(string.Empty, GetField(app, "pendingTemporaryPasswordNickname"));
                Assert.AreEqual(false, GetField(app, "temporaryPasswordRevealVisible"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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

        private static void Invoke(RaidGameApp app, string methodName)
        {
            var method = typeof(RaidGameApp).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(app, null);
        }
    }
}
