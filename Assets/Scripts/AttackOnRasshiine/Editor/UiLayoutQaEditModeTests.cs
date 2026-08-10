using System.Linq;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Editor
{
    public sealed class UiLayoutQaEditModeTests
    {
        [Test]
        public void ScanFindsSiblingTextOverlap()
        {
            var root = CreateRoot();
            try
            {
                CreateText(root, "First", new Vector2(-18f, 0f), new Vector2(100f, 34f));
                CreateText(root, "Second", new Vector2(18f, 0f), new Vector2(100f, 34f));

                var issues = UiLayoutQa.Scan(root);

                Assert.That(issues.Any(issue => issue.Contains("overlaps sibling")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void ScanFindsViewportEscape()
        {
            var root = CreateRoot();
            try
            {
                CreateText(root, "Outside", new Vector2(180f, 0f), new Vector2(100f, 34f));

                var issues = UiLayoutQa.Scan(root);

                Assert.That(issues.Any(issue => issue.Contains("leaves the viewport")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void ScanFindsUndersizedButtonTarget()
        {
            var root = CreateRoot();
            try
            {
                var buttonObject = new GameObject("TinyButton", typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.SetParent(root, false);
                rect.sizeDelta = new Vector2(80f, 30f);

                var issues = UiLayoutQa.Scan(root);

                Assert.That(issues.Any(issue => issue.Contains("minimum is 44x44")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void ScanIgnoresExclusiveInputTextAndPlaceholderPair()
        {
            var root = CreateRoot();
            try
            {
                var inputObject = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
                var inputRect = inputObject.GetComponent<RectTransform>();
                inputRect.SetParent(root, false);
                inputRect.sizeDelta = new Vector2(180f, 52f);

                CreateText(inputRect, "Text", Vector2.zero, new Vector2(150f, 40f));
                CreateText(inputRect, "Placeholder", Vector2.zero, new Vector2(150f, 40f));
                var input = inputObject.GetComponent<InputField>();
                input.textComponent = inputRect.Find("Text").GetComponent<Text>();
                input.placeholder = inputRect.Find("Placeholder").GetComponent<Text>();
                input.characterLimit = 64;

                var issues = UiLayoutQa.Scan(root);

                Assert.That(issues.Any(issue => issue.Contains("overlaps sibling")), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        private static RectTransform CreateRoot()
        {
            var root = new GameObject("QaRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(300f, 200f);
            return root;
        }

        private static void CreateText(RectTransform root, string name, Vector2 position, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.text = name;
            text.alignment = TextAnchor.MiddleCenter;
        }
    }
}
