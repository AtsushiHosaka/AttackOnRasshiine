using AttackOnRasshiine.Runtime.UI;
using Michsky.UI.Heat;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Editor
{
    public sealed class NeonUiFactoryHeatButtonEditModeTests
    {
        [Test]
        public void HeatButtonInvokesCallbackFromUnityButtonAndHeatManager()
        {
            var root = new GameObject("HeatButtonTestRoot", typeof(RectTransform));
            var themeObject = new GameObject("HeatButtonTestTheme");
            try
            {
                var theme = CreateHeatTheme(themeObject);
                var ui = new NeonUiFactory(theme);
                var clickCount = 0;

                var button = ui.CreateButton(root.transform, "StrongAttackButton", "強攻", null, () => clickCount++);
                var manager = button.GetComponent<ButtonManager>() ?? button.GetComponentInChildren<ButtonManager>(true);

                Assert.IsNotNull(manager);
                button.onClick.Invoke();
                manager.InvokeOnClick();

                Assert.AreEqual(2, clickCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(themeObject);
            }
        }

        [Test]
        public void HeatButtonChildrenDoNotStealRaycastsFromRoot()
        {
            var root = new GameObject("HeatButtonRaycastRoot", typeof(RectTransform));
            var themeObject = new GameObject("HeatButtonRaycastTheme");
            try
            {
                var theme = CreateHeatTheme(themeObject);
                var ui = new NeonUiFactory(theme);
                var button = ui.CreateButton(root.transform, "RaycastButton", "ログイン", null, () => { });
                var rootImage = button.GetComponent<Image>();

                Assert.IsNotNull(rootImage);
                Assert.IsTrue(rootImage.raycastTarget);
                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.gameObject == button.gameObject)
                    {
                        continue;
                    }

                    Assert.IsFalse(graphic.raycastTarget, $"{graphic.name} should not block the Heat button root.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(themeObject);
            }
        }

        [Test]
        public void HeatButtonLabelIgnoresLayoutSoCompactHudButtonsKeepTextVisible()
        {
            var root = new GameObject("HeatButtonLayoutRoot", typeof(RectTransform));
            var themeObject = new GameObject("HeatButtonLayoutTheme");
            try
            {
                var theme = CreateHeatTheme(themeObject);
                var ui = new NeonUiFactory(theme);

                var button = ui.CreateButton(root.transform, "CompactHudButton", "攻撃", null, () => { });
                var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == "CompactHudButton_Label");

                Assert.IsNotNull(label);
                var layout = label.GetComponent<LayoutElement>();
                Assert.IsNotNull(layout);
                Assert.IsTrue(layout.ignoreLayout);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(themeObject);
            }
        }

        private static RasshiineTheme CreateHeatTheme(GameObject themeObject)
        {
            var theme = themeObject.AddComponent<RasshiineTheme>();
            theme.UseHeatUiSkin = true;
            theme.HeatButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.HeatButtonPrefabPath);
            Assert.IsNotNull(theme.HeatButtonPrefab, "Heat UI button prefab must be present.");
            return theme;
        }
    }
}
