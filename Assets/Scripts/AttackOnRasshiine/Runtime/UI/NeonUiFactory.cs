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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

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
            var resolvedSprite = sprite != null ? sprite : theme.RaidPanel;
            ApplySprite(image, resolvedSprite);
            image.color = PanelColor(resolvedSprite);
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
            var button = CreateButtonFrame(parent, name, sprite, onClick);
            var labelText = CreateText(button.transform, $"{name}_Label", label, FontSizeForButton(label), FontStyle.Bold, labelColor ?? theme.Text, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform, 22, 10, -22, -10);
            return button;
        }

        public Button CreateIconButton(Transform parent, string name, Sprite icon, Sprite sprite, UnityAction onClick, Color? iconColor = null)
        {
            var button = CreateButtonFrame(parent, name, sprite, onClick);
            var iconObject = new GameObject($"{name}_Icon", typeof(Image));
            iconObject.transform.SetParent(button.transform, false);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.color = iconColor ?? theme.Text;
            Stretch(iconImage.rectTransform, 18, 18, -18, -18);
            return button;
        }

        public InputField CreateInput(Transform parent, string name, string placeholder, bool multiline = false)
        {
            var root = new GameObject(name, typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            ApplySprite(image, theme.InputField != null ? theme.InputField : theme.StatCard);
            image.color = theme.UseHeatUiSkin
                ? new Color(0.035f, 0.07f, 0.15f, 0.92f)
                : new Color(1f, 1f, 1f, 0.86f);

            var input = root.GetComponent<InputField>();
            input.targetGraphic = image;
            ConfigureSelectableColors(input, image.color, new Color(0.1f, 0.2f, 0.34f, 0.98f), new Color(0.08f, 0.18f, 0.3f, 1f));
            input.caretColor = theme.Cyan;
            input.selectionColor = new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.34f);
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
            ApplySprite(bgImage, theme.SliderFrame != null ? theme.SliderFrame : theme.ProgressFrame);
            bgImage.color = theme.UseHeatUiSkin ? new Color(0.07f, 0.1f, 0.2f, 0.95f) : Color.white;
            Stretch(bgImage.rectTransform, 0, 0, 0, 0);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 20, 15, -20, -15);

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.GetComponent<Image>();
            ApplySprite(fillImage, theme.SliderFill != null ? theme.SliderFill : theme.ProgressFillCyan);
            fillImage.color = theme.UseHeatUiSkin ? theme.Cyan : Color.white;
            Stretch(fillImage.rectTransform, 0, 0, 0, 0);

            var handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(root.transform, false);
            var handleImage = handle.GetComponent<Image>();
            handleImage.sprite = theme.SliderHandle != null ? theme.SliderHandle : theme.HexBadge;
            handleImage.preserveAspect = true;
            handleImage.color = theme.UseHeatUiSkin ? theme.Gold : new Color(1f, 1f, 1f, 0.95f);
            var handleRect = handleImage.rectTransform;
            handleRect.sizeDelta = new Vector2(42, 42);

            slider.fillRect = fillImage.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        public RectTransform CreateProgressBar(Transform parent, string name, float value01, bool magenta = false)
        {
            var root = CreatePanel(parent, name, theme.SliderFrame != null ? theme.SliderFrame : theme.ProgressFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(root, false);
            var fillImage = fillObject.GetComponent<Image>();
            ApplySprite(fillImage, theme.SliderFill != null ? theme.SliderFill : magenta ? theme.ProgressFillMagenta : theme.ProgressFillCyan);
            fillImage.color = theme.UseHeatUiSkin ? magenta ? theme.Magenta : theme.Cyan : Color.white;
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

        private Button CreateButtonFrame(Transform parent, string name, Sprite sprite, UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            var resolvedSprite = sprite != null ? sprite : theme.PrimaryButton;
            ApplySprite(image, resolvedSprite);
            var normalColor = ButtonColor(resolvedSprite);
            image.color = normalColor;

            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            ConfigureSelectableColors(button, normalColor, HighlightColor(normalColor), PressedColor(normalColor));
            button.onClick.AddListener(onClick);
            return button;
        }

        private void ApplySprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }

        private void ConfigureSelectableColors(Selectable selectable, Color normal, Color highlighted, Color pressed)
        {
            var colors = selectable.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.selectedColor = highlighted;
            colors.pressedColor = pressed;
            colors.disabledColor = new Color(0.24f, 0.27f, 0.34f, 0.72f);
            selectable.colors = colors;
        }

        private Color PanelColor(Sprite sprite)
        {
            if (!theme.UseHeatUiSkin)
            {
                return Color.white;
            }

            if (sprite == theme.NotificationPanel)
            {
                return new Color(0.08f, 0.13f, 0.2f, 0.96f);
            }

            if (sprite == theme.StatCard)
            {
                return new Color(0.045f, 0.085f, 0.16f, 0.92f);
            }

            if (sprite == theme.LogPanel)
            {
                return new Color(0.025f, 0.045f, 0.105f, 0.9f);
            }

            return new Color(0.045f, 0.07f, 0.15f, 0.94f);
        }

        private Color ButtonColor(Sprite sprite)
        {
            if (!theme.UseHeatUiSkin)
            {
                return Color.white;
            }

            if (sprite == theme.DangerButton)
            {
                return new Color(0.48f, 0.1f, 0.24f, 0.96f);
            }

            if (sprite == theme.SecondaryButton)
            {
                return new Color(0.07f, 0.1f, 0.18f, 0.94f);
            }

            return new Color(0.02f, 0.32f, 0.42f, 0.96f);
        }

        private static Color HighlightColor(Color color)
        {
            return new Color(
                Mathf.Min(color.r + 0.12f, 1f),
                Mathf.Min(color.g + 0.16f, 1f),
                Mathf.Min(color.b + 0.2f, 1f),
                color.a);
        }

        private static Color PressedColor(Color color)
        {
            return new Color(
                Mathf.Min(color.r + 0.28f, 1f),
                Mathf.Max(color.g - 0.02f, 0f),
                Mathf.Min(color.b + 0.24f, 1f),
                color.a);
        }

        private static int FontSizeForButton(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return 28;
            }

            if (label.Length >= 11)
            {
                return 21;
            }

            return label.Length >= 7 ? 24 : 28;
        }
    }
}
