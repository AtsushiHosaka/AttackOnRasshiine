using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Runtime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class UiTextFitGuard : MonoBehaviour
    {
        private const int MaxFitIterations = 24;
        private const float FitTolerance = 0.75f;

        [SerializeField] private int maxCharacters;
        [SerializeField] private bool preserveFullText;
        [SerializeField] private int minimumFontSize;

        private Text targetText;
        private string sourceText = string.Empty;
        private string lastAppliedText = string.Empty;
        private Rect lastRect;
        private int lastFontSize;
        private bool applying;
        private bool configured;
        private int maximumFontSize;

        public void Configure(int newMaxCharacters = 0)
        {
            maxCharacters = Mathf.Max(0, newMaxCharacters);
            ResolveText();
            sourceText = targetText != null ? targetText.text ?? string.Empty : string.Empty;
            lastAppliedText = string.Empty;
            configured = true;
        }

        public void ConfigurePreserveFullText(int newMinimumFontSize)
        {
            maxCharacters = 0;
            preserveFullText = true;
            ResolveText();
            maximumFontSize = targetText != null ? targetText.fontSize : 0;
            minimumFontSize = Mathf.Clamp(newMinimumFontSize, 8, Mathf.Max(8, maximumFontSize));
            sourceText = targetText != null ? targetText.text ?? string.Empty : string.Empty;
            lastAppliedText = string.Empty;
            lastRect = default;
            lastFontSize = 0;
            configured = true;
        }

        public void FitNow()
        {
            ResolveText();
            if (!configured || targetText == null || ShouldSkipFitting(targetText))
            {
                return;
            }

            var rect = targetText.rectTransform.rect;
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            if (!applying && targetText.text != lastAppliedText)
            {
                sourceText = targetText.text ?? string.Empty;
            }

            if (rect == lastRect && targetText.fontSize == lastFontSize && targetText.text == lastAppliedText)
            {
                return;
            }

            var candidate = ApplyCharacterLimit(sourceText, maxCharacters);
            if (preserveFullText)
            {
                FitFullTextByFontSize(candidate, rect);
            }
            else if (!TextFits(targetText, candidate, rect))
            {
                candidate = FitToRect(candidate, rect);
            }

            Apply(candidate);
            lastRect = rect;
            lastFontSize = targetText.fontSize;
        }

        private void FitFullTextByFontSize(string value, Rect rect)
        {
            if (targetText == null)
            {
                return;
            }

            var upper = Mathf.Max(minimumFontSize, maximumFontSize > 0 ? maximumFontSize : targetText.fontSize);
            var lower = Mathf.Clamp(minimumFontSize, 8, upper);
            var best = lower;
            var low = lower;
            var high = upper;
            while (low <= high)
            {
                var candidateSize = (low + high) / 2;
                targetText.fontSize = candidateSize;
                if (TextFits(targetText, value, rect))
                {
                    best = candidateSize;
                    low = candidateSize + 1;
                }
                else
                {
                    high = candidateSize - 1;
                }
            }

            targetText.fontSize = best;
            targetText.resizeTextForBestFit = false;
        }

        public bool CurrentTextFits()
        {
            ResolveText();
            if (targetText == null || ShouldSkipFitting(targetText))
            {
                return true;
            }

            var rect = targetText.rectTransform.rect;
            return rect.width < 2f || rect.height < 2f || TextFits(targetText, targetText.text ?? string.Empty, rect);
        }

        public static bool TextFits(Text text, string value, Rect rect)
        {
            if (text == null || string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (rect.width < 2f || rect.height < 2f)
            {
                return true;
            }

            var settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, rect.width), Mathf.Max(1f, rect.height)));
            var height = text.cachedTextGeneratorForLayout.GetPreferredHeight(value, settings) / text.pixelsPerUnit;
            var heightFits = height <= rect.height + FitTolerance;
            if (!heightFits)
            {
                return false;
            }

            if (text.horizontalOverflow == HorizontalWrapMode.Wrap)
            {
                return true;
            }

            var width = text.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / text.pixelsPerUnit;
            return width <= rect.width + FitTolerance;
        }

        private void Awake()
        {
            ResolveText();
            sourceText = targetText != null ? targetText.text ?? string.Empty : string.Empty;
        }

        private void OnEnable()
        {
            ResolveText();
            if (!configured)
            {
                return;
            }

            if (targetText != null && (string.IsNullOrEmpty(sourceText) || targetText.text != lastAppliedText))
            {
                sourceText = targetText.text ?? string.Empty;
            }

            FitNow();
        }

        private void LateUpdate()
        {
            FitNow();
        }

        private void ResolveText()
        {
            if (targetText == null)
            {
                targetText = GetComponent<Text>();
            }
        }

        private string FitToRect(string value, Rect rect)
        {
            if (string.IsNullOrEmpty(value) || TextFits(targetText, value, rect))
            {
                return value;
            }

            var low = 0;
            var high = Mathf.Max(0, value.Length - 1);
            var best = string.Empty;
            for (var iteration = 0; iteration < MaxFitIterations && low <= high; iteration++)
            {
                var mid = (low + high) / 2;
                var candidate = TrimWithEllipsis(value, mid);
                if (TextFits(targetText, candidate, rect))
                {
                    best = candidate;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return string.IsNullOrEmpty(best) ? "…" : best;
        }

        private void Apply(string value)
        {
            applying = true;
            targetText.text = value;
            lastAppliedText = value;
            applying = false;
        }

        private static bool ShouldSkipFitting(Text text)
        {
            var input = text.GetComponentInParent<InputField>();
            return input != null && input.textComponent == text;
        }

        private static string ApplyCharacterLimit(string value, int limit)
        {
            if (limit <= 0 || string.IsNullOrEmpty(value) || value.Length <= limit)
            {
                return value ?? string.Empty;
            }

            return TrimWithEllipsis(value, Mathf.Max(0, limit - 1));
        }

        private static string TrimWithEllipsis(string value, int visibleCharacters)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var count = Mathf.Clamp(visibleCharacters, 0, value.Length);
            if (count > 0 && char.IsHighSurrogate(value[count - 1]))
            {
                count -= 1;
            }

            return count <= 0 ? "…" : $"{value.Substring(0, count).TrimEnd()}…";
        }
    }
}
