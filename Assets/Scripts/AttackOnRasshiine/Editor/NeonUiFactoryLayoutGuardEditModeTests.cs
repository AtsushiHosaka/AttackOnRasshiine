using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Editor
{
    public sealed class NeonUiFactoryLayoutGuardEditModeTests
    {
        [Test]
        public void CreatedTextHasFitGuardAndLongSingleLineIsTrimmedToItsRect()
        {
            var root = new GameObject("TextFitGuardTestRoot", typeof(RectTransform));
            try
            {
                var theme = new GameObject("TextFitGuardTheme").AddComponent<RasshiineTheme>();
                var ui = new NeonUiFactory(theme);
                var text = ui.CreateText(root.transform, "LongLabel", "これはかなり長いラベルで小さな枠から確実にはみ出すテキストです", 24, FontStyle.Bold, theme.Text, TextAnchor.MiddleCenter);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.rectTransform.sizeDelta = new Vector2(96f, 28f);

                var guard = text.GetComponent<UiTextFitGuard>();
                Assert.IsNotNull(guard);

                guard.FitNow();

                StringAssert.EndsWith("…", text.text);
                Assert.IsTrue(guard.CurrentTextFits(), text.text);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InputsHavePurposeSpecificCharacterLimits()
        {
            var root = new GameObject("InputLimitTestRoot", typeof(RectTransform));
            try
            {
                var theme = new GameObject("InputLimitTheme").AddComponent<RasshiineTheme>();
                var ui = new NeonUiFactory(theme);

                var login = ui.CreateInput(root.transform, "LoginIdInput", "ログインID");
                var password = ui.CreateInput(root.transform, "PasswordInput", "パスワード");
                var goal = ui.CreateInput(root.transform, "GoalInput", "今日の目標");
                var url = ui.CreateInput(root.transform, "ProductUrlInput", "https://example.com");
                var reflection = ui.CreateInput(root.transform, "ReflectionInput", "ふりかえり", true);

                Assert.AreEqual(32, login.characterLimit);
                Assert.AreEqual(72, password.characterLimit);
                Assert.AreEqual(90, goal.characterLimit);
                Assert.AreEqual(200, url.characterLimit);
                Assert.AreEqual(240, reflection.characterLimit);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
