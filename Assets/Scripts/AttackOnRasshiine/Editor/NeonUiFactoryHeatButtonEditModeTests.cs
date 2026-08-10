using AttackOnRasshiine.Runtime.UI;
using Michsky.UI.Heat;
using NUnit.Framework;
using System.Linq;
using TMPro;
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
        public void HeatButtonLabelUsesBakedImageInsteadOfTextOverlay()
        {
            var root = new GameObject("HeatButtonLayoutRoot", typeof(RectTransform));
            var themeObject = new GameObject("HeatButtonLayoutTheme");
            try
            {
                var theme = CreateHeatTheme(themeObject);
                var ui = new NeonUiFactory(theme);

                var button = ui.CreateButton(root.transform, "CompactHudButton", "攻撃", null, () => { });
                var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name.Contains("Label"));
                var tmpLabel = button.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(text => text.name.Contains("Label"));
                var bakedImage = button.GetComponentsInChildren<BakedTextButtonImage>(true).FirstOrDefault();

                Assert.IsNull(label, "Button labels should be baked into an image instead of rendered as a live Text overlay.");
                Assert.IsNull(tmpLabel, "Button labels should not leave a TMP text overlay either.");
                Assert.IsNotNull(bakedImage);
                Assert.AreEqual("攻撃", bakedImage.Label);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(themeObject);
            }
        }

        [Test]
        public void Option3PrimaryButtonUsesGeneratedCtaWithLiveText()
        {
            var root = new GameObject("Option3ButtonLayoutRoot", typeof(RectTransform));
            var themeObject = new GameObject("Option3ButtonLayoutTheme");
            try
            {
                var theme = themeObject.AddComponent<RasshiineTheme>();
                theme.UseHeatUiSkin = true;
                theme.UseOption3LiveUi = true;
                theme.PrimaryButton = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Primary.png");
                theme.LoginCtaButton = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginCtaButtonPath);
                Assert.IsNotNull(theme.PrimaryButton, "The semantic primary button sprite must be present for the option-3 mapping test.");
                Assert.IsNotNull(theme.LoginCtaButton, "The generated option-3 CTA sprite must be present.");

                var ui = new NeonUiFactory(theme);
                var button = ui.CreateButton(root.transform, "Option3PrimaryButton", "開発をはじめる", theme.PrimaryButton, () => { });
                var image = button.GetComponent<Image>();
                var liveLabel = button.GetComponentsInChildren<Text>(true).SingleOrDefault();

                Assert.IsNotNull(image);
                Assert.AreSame(theme.LoginCtaButton, image.sprite, "Primary actions must map to the approved option-3 CTA art.");
                Assert.AreEqual(Image.Type.Sliced, image.type);
                Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1.5f).Within(0.001f), "The CTA 9-slice must use enough PPU compensation to preserve its label-safe center.");
                Assert.IsNotNull(liveLabel, "Option-3 buttons must retain selectable, WebGL-friendly live Text.");
                Assert.AreEqual("開発をはじめる", liveLabel.text);
                Assert.IsFalse(liveLabel.raycastTarget, "The live label must not intercept the button root's pointer events.");
                Assert.IsNull(button.GetComponentInChildren<BakedTextButtonImage>(true), "Option-3 buttons must not generate runtime baked label textures.");
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
            theme.UseOption3LiveUi = false;
            theme.HeatButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.HeatButtonPrefabPath);
            Assert.IsNotNull(theme.HeatButtonPrefab, "Heat UI button prefab must be present.");
            return theme;
        }
    }
}
