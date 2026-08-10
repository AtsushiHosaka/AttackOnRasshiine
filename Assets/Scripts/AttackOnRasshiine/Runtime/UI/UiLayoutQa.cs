using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Runtime.UI
{
    public static class UiLayoutQa
    {
        public static IReadOnlyList<string> Scan(Transform root)
        {
            var issues = new List<string>();
            if (root == null)
            {
                issues.Add("UI root is null.");
                return issues;
            }

            Canvas.ForceUpdateCanvases();
            if (root is RectTransform rootRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            }

            foreach (var guard in root.GetComponentsInChildren<UiTextFitGuard>(true))
            {
                guard.FitNow();
            }

            Canvas.ForceUpdateCanvases();
            if (root is RectTransform rebuiltRoot)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rebuiltRoot);
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (!text.gameObject.activeInHierarchy || ShouldSkipText(text))
                {
                    continue;
                }

                var rect = text.rectTransform.rect;
                if (rect.width < 2f || rect.height < 2f)
                {
                    continue;
                }

                var guard = text.GetComponent<UiTextFitGuard>();
                var fits = guard != null
                    ? guard.CurrentTextFits()
                    : UiTextFitGuard.TextFits(text, text.text ?? string.Empty, rect);
                if (!fits)
                {
                    issues.Add($"{GetPath(text.transform)} text does not fit {rect.width:0.#}x{rect.height:0.#}: \"{Preview(text.text)}\"");
                }
            }

            foreach (var input in root.GetComponentsInChildren<InputField>(true))
            {
                if (!input.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (input.characterLimit <= 0)
                {
                    issues.Add($"{GetPath(input.transform)} input has no character limit.");
                }
            }

            ScanViewportContainment(root, issues);
            ScanButtonTargets(root, issues);
            ScanSiblingCollisions(root, issues);

            return issues;
        }

        private static void ScanViewportContainment(Transform root, ICollection<string> issues)
        {
            if (root is not RectTransform rootRect)
            {
                return;
            }

            var viewport = WorldRect(rootRect);
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == rootRect || !rect.gameObject.activeInHierarchy || rect.rect.width < 2f || rect.rect.height < 2f)
                {
                    continue;
                }

                var isQaTarget = rect.GetComponent<Text>() != null ||
                                 rect.GetComponent<Button>() != null ||
                                 rect.GetComponent<InputField>() != null;
                if (!isQaTarget || rect.GetComponentInParent<ScrollRect>() != null)
                {
                    continue;
                }

                var bounds = WorldRect(rect);
                const float tolerance = 1.5f;
                if (bounds.xMin < viewport.xMin - tolerance ||
                    bounds.yMin < viewport.yMin - tolerance ||
                    bounds.xMax > viewport.xMax + tolerance ||
                    bounds.yMax > viewport.yMax + tolerance)
                {
                    issues.Add($"{GetPath(rect)} leaves the viewport: {bounds.width:0.#}x{bounds.height:0.#}");
                }
            }
        }

        private static void ScanButtonTargets(Transform root, ICollection<string> issues)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rect = button.GetComponent<RectTransform>();
                if (rect == null || rect.rect.width < 2f || rect.rect.height < 2f)
                {
                    continue;
                }

                var canvas = rect.GetComponentInParent<Canvas>();
                var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                var renderedWidth = rect.rect.width * scale;
                var renderedHeight = rect.rect.height * scale;
                if (renderedWidth < 44f || renderedHeight < 44f)
                {
                    issues.Add($"{GetPath(rect)} touch target is {renderedWidth:0.#}x{renderedHeight:0.#} px; minimum is 44x44 px.");
                }
            }
        }

        private static void ScanSiblingCollisions(Transform root, ICollection<string> issues)
        {
            foreach (var parent in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!parent.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var targets = new List<RectTransform>();
                for (var index = 0; index < parent.childCount; index++)
                {
                    if (parent.GetChild(index) is not RectTransform child || !child.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (child.GetComponent<Text>() != null || child.GetComponent<Button>() != null || child.GetComponent<InputField>() != null)
                    {
                        targets.Add(child);
                    }
                }

                for (var leftIndex = 0; leftIndex < targets.Count; leftIndex++)
                {
                    var left = targets[leftIndex];
                    var leftBounds = WorldRect(left);
                    for (var rightIndex = leftIndex + 1; rightIndex < targets.Count; rightIndex++)
                    {
                        var right = targets[rightIndex];
                        if (IsExclusiveInputTextPair(parent, left, right))
                        {
                            continue;
                        }

                        var rightBounds = WorldRect(right);
                        var overlapWidth = Mathf.Min(leftBounds.xMax, rightBounds.xMax) - Mathf.Max(leftBounds.xMin, rightBounds.xMin);
                        var overlapHeight = Mathf.Min(leftBounds.yMax, rightBounds.yMax) - Mathf.Max(leftBounds.yMin, rightBounds.yMin);
                        if (overlapWidth > 2f && overlapHeight > 2f)
                        {
                            issues.Add($"{GetPath(left)} overlaps sibling {right.name} by {overlapWidth:0.#}x{overlapHeight:0.#} px.");
                        }
                    }
                }
            }
        }

        private static bool IsExclusiveInputTextPair(RectTransform parent, RectTransform left, RectTransform right)
        {
            var input = parent != null ? parent.GetComponent<InputField>() : null;
            if (input == null)
            {
                return false;
            }

            var text = input.textComponent != null ? input.textComponent.rectTransform : null;
            var placeholder = input.placeholder != null ? input.placeholder.rectTransform : null;
            return (left == text && right == placeholder) || (left == placeholder && right == text);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var xMin = corners.Min(corner => corner.x);
            var xMax = corners.Max(corner => corner.x);
            var yMin = corners.Min(corner => corner.y);
            var yMax = corners.Max(corner => corner.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool ShouldSkipText(Text text)
        {
            var input = text.GetComponentInParent<InputField>();
            return input != null && input.textComponent == text;
        }

        private static string Preview(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return normalized.Length <= 48 ? normalized : $"{normalized.Substring(0, 47)}…";
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            for (var current = transform.parent; current != null; current = current.parent)
            {
                path = $"{current.name}/{path}";
            }

            return path;
        }
    }
}
