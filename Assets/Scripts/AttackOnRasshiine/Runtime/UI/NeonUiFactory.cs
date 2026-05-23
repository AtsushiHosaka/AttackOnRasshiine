using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class NeonUiFactory
    {
        private readonly RasshiineTheme theme;
        private readonly Font font;

        public NeonUiFactory(RasshiineTheme theme)
        {
            this.theme = theme;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Hiragino Sans", "Yu Gothic", "Arial", "Helvetica" }, 18);
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        public Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public RectTransform CreatePanel(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var panel = new GameObject(name, typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = panel.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : theme.RaidPanel;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            return rect;
        }

        public Text CreateText(Transform parent, string name, string value, int size, FontStyle style, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public Button CreateButton(Transform parent, string name, string label, Sprite sprite, UnityAction onClick, Color? labelColor = null)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : theme.PrimaryButton;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.75f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(1f, 0.45f, 0.9f, 1f);
            colors.disabledColor = new Color(0.35f, 0.38f, 0.48f, 0.7f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var labelText = CreateText(buttonObject.transform, $"{name}_Label", label, 28, FontStyle.Bold, labelColor ?? theme.Text, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform, 28, 12, -28, -12);
            return button;
        }

        public InputField CreateInput(Transform parent, string name, string placeholder, bool multiline = false)
        {
            var root = new GameObject(name, typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.sprite = theme.StatCard;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.86f);

            var input = root.GetComponent<InputField>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.textComponent = CreateText(root.transform, $"{name}_Text", string.Empty, 24, FontStyle.Normal, theme.Text, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            Stretch(input.textComponent.rectTransform, 28, 12, -28, -12);

            var placeholderText = CreateText(root.transform, $"{name}_Placeholder", placeholder, 24, FontStyle.Normal, theme.MutedText, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            placeholderText.fontStyle = FontStyle.Italic;
            Stretch(placeholderText.rectTransform, 28, 12, -28, -12);
            input.placeholder = placeholderText;
            return input;
        }

        public Slider CreateSlider(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(Slider));
            root.transform.SetParent(parent, false);
            var slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 75f;

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(root.transform, false);
            var bgImage = background.GetComponent<Image>();
            bgImage.sprite = theme.ProgressFrame;
            bgImage.type = Image.Type.Sliced;
            Stretch(bgImage.rectTransform, 0, 0, 0, 0);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 20, 15, -20, -15);

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = theme.ProgressFillCyan;
            fillImage.type = Image.Type.Sliced;
            Stretch(fillImage.rectTransform, 0, 0, 0, 0);

            var handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(root.transform, false);
            var handleImage = handle.GetComponent<Image>();
            handleImage.sprite = theme.HexBadge;
            handleImage.color = new Color(1f, 1f, 1f, 0.95f);
            var handleRect = handleImage.rectTransform;
            handleRect.sizeDelta = new Vector2(42, 42);

            slider.fillRect = fillImage.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        public RectTransform CreateProgressBar(Transform parent, string name, float value01, bool magenta = false)
        {
            var root = CreatePanel(parent, name, theme.ProgressFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(root, false);
            var fillImage = fillObject.GetComponent<Image>();
            fillImage.sprite = magenta ? theme.ProgressFillMagenta : theme.ProgressFillCyan;
            fillImage.type = Image.Type.Sliced;
            fillImage.rectTransform.anchorMin = new Vector2(0f, 0f);
            fillImage.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            fillImage.rectTransform.offsetMin = new Vector2(22, 16);
            fillImage.rectTransform.offsetMax = new Vector2(-22, -16);
            return root;
        }

        public void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        public void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public void Clear(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(index).gameObject);
            }
        }

        public static string Percent(float value)
        {
            return $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
