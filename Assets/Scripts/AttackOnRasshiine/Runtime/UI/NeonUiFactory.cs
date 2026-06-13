using System;
using System.Reflection;
using Michsky.UI.Heat;
using TMPro;
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
            font = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Noto Sans JP",
                    "Noto Sans CJK JP",
                    "Source Han Sans JP",
                    "Hiragino Sans",
                    "Hiragino Kaku Gothic ProN",
                    "Helvetica Neue",
                    "SF Pro Text",
                    "SF Pro Display",
                    "Yu Gothic UI",
                    "Yu Gothic",
                    "Meiryo",
                    "Hiragino Sans W6",
                    "Arial",
                    "Helvetica"
                },
                18);
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
            canvasObject.AddComponent<CanvasManager>();

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
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
            image.color = theme.UseHeatUiSkin ? PanelFrameColor(resolvedSprite) : PanelColor(resolvedSprite);
            if (theme.UseHeatUiSkin)
            {
                AddHeatPanelFill(rect, resolvedSprite);
            }
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
            text.alignByGeometry = true;
            text.lineSpacing = 1f;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public Button CreateButton(Transform parent, string name, string label, Sprite sprite, UnityAction onClick, Color? labelColor = null)
        {
            if (theme.UseHeatUiSkin && TryCreateHeatPrefabButton(parent, name, label, sprite, onClick, labelColor ?? theme.Text, out var heatButton))
            {
                return heatButton;
            }

            var button = CreateButtonFrame(parent, name, sprite, onClick);
            if (theme.UseHeatUiSkin)
            {
                ConfigureHeatButton(button, name, sprite);
            }

            var labelText = CreateText(button.transform, $"{name}_Label", label, FontSizeForButton(label), FontStyle.Bold, labelColor ?? theme.Text, TextAnchor.MiddleCenter);
            labelText.raycastTarget = !theme.UseHeatUiSkin;
            IgnoreLayout(labelText.gameObject);
            Stretch(labelText.rectTransform, 22, 10, -22, -10);
            return button;
        }

        public Button CreateIconButton(Transform parent, string name, Sprite icon, Sprite sprite, UnityAction onClick, Color? iconColor = null)
        {
            if (theme.UseHeatUiSkin && TryCreateHeatPrefabButton(parent, name, string.Empty, sprite, onClick, iconColor ?? theme.Text, out var heatButton))
            {
                AddIconImage(heatButton.transform, name, icon, iconColor);
                return heatButton;
            }

            var button = CreateButtonFrame(parent, name, sprite, onClick);
            if (theme.UseHeatUiSkin)
            {
                ConfigureHeatButton(button, name, sprite);
            }

            AddIconImage(button.transform, name, icon, iconColor);
            return button;
        }

        private void AddIconImage(Transform parent, string name, Sprite icon, Color? iconColor = null)
        {
            var iconObject = new GameObject($"{name}_Icon", typeof(Image));
            iconObject.transform.SetParent(parent, false);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.color = iconColor ?? theme.Text;
            IgnoreLayout(iconObject);
            Stretch(iconImage.rectTransform, 18, 18, -18, -18);
        }

        public InputField CreateInput(Transform parent, string name, string placeholder, bool multiline = false)
        {
            var root = new GameObject(name, typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            ApplySprite(image, theme.InputField != null ? theme.InputField : theme.StatCard);
            var useHeatInputPrefab = theme.UseHeatUiSkin && theme.HeatInputFieldPrefab != null;
            image.color = useHeatInputPrefab
                ? new Color(0f, 0f, 0f, 0.01f)
                : theme.UseHeatUiSkin
                ? theme.Cyan
                : new Color(1f, 1f, 1f, 0.86f);
            if (theme.UseHeatUiSkin)
            {
                if (!TryAddHeatInputPrefabVisual(root.transform))
                {
                    AddHeatInsetFill(root.transform, "InputFill", new Color(0.015f, 0.022f, 0.045f, 0.9f), 7f);
                    AddHeatAccentLine(root.transform, "InputAccent", theme.Cyan, true);
                }
            }

            var input = root.GetComponent<InputField>();
            input.targetGraphic = image;
            ConfigureSelectableColors(input, image.color, new Color(0.1f, 0.2f, 0.34f, 0.98f), new Color(0.08f, 0.18f, 0.3f, 1f));
            input.caretColor = theme.Cyan;
            input.selectionColor = new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.34f);
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.textComponent = CreateText(root.transform, $"{name}_Text", string.Empty, 24, FontStyle.Normal, theme.Text, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            Stretch(input.textComponent.rectTransform, 28, 12, -28, -12);

            var placeholderText = CreateText(root.transform, $"{name}_Placeholder", placeholder, 24, FontStyle.Normal, InputPlaceholderColor(), multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
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
            handleImage.color = theme.UseHeatUiSkin ? theme.Cyan : new Color(1f, 1f, 1f, 0.95f);
            var handleRect = handleImage.rectTransform;
            handleRect.sizeDelta = new Vector2(42, 42);

            slider.fillRect = fillImage.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        public RectTransform CreateProgressBar(Transform parent, string name, float value01, bool magenta = false)
        {
            if (theme.UseHeatUiSkin && TryCreateHeatPrefabProgressBar(parent, name, value01, magenta, out var heatProgress))
            {
                return heatProgress;
            }

            var root = CreatePanel(parent, name, theme.SliderFrame != null ? theme.SliderFrame : theme.ProgressFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(root, false);
            var fillImage = fillObject.GetComponent<Image>();
            ApplySprite(fillImage, theme.SliderFill != null ? theme.SliderFill : magenta ? theme.ProgressFillMagenta : theme.ProgressFillCyan);
            fillImage.color = theme.UseHeatUiSkin ? magenta ? theme.Magenta : theme.Cyan : Color.white;
            fillImage.rectTransform.anchorMin = new Vector2(0f, 0f);
            fillImage.rectTransform.offsetMin = new Vector2(22, 16);
            fillImage.rectTransform.offsetMax = new Vector2(-22, -16);
            if (theme.UseHeatUiSkin)
            {
                fillImage.rectTransform.anchorMax = new Vector2(1f, 1f);
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillAmount = Mathf.Clamp01(value01);
                var progress = root.gameObject.AddComponent<ProgressBar>();
                progress.barImage = fillImage;
                progress.minValue = 0f;
                progress.maxValue = 100f;
                progress.currentValue = Mathf.Clamp01(value01) * 100f;
                progress.addSuffix = false;
                progress.UpdateUI();
            }
            else
            {
                fillImage.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            }
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

        private bool TryCreateHeatPrefabButton(Transform parent, string name, string label, Sprite sprite, UnityAction onClick, Color labelColor, out Button button)
        {
            button = null;
            if (theme.HeatButtonPrefab == null)
            {
                return false;
            }

            var instance = UnityEngine.Object.Instantiate(theme.HeatButtonPrefab, parent, false);
            instance.name = name;
            var rect = instance.GetComponent<RectTransform>() ?? instance.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;

            DisableHeatAutoSizing(instance);
            DisableTmpText(instance.transform);
            FreezeHeatUiManagerImages(instance);
            ApplyHeatButtonPalette(instance, sprite);

            var manager = instance.GetComponent<ButtonManager>() ?? instance.GetComponentInChildren<ButtonManager>(true);
            if (manager == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            manager.buttonText = string.Empty;
            manager.enableText = false;
            manager.enableIcon = false;
            manager.useLocalization = false;
            manager.useSounds = false;
            manager.checkForDoubleClick = false;
            manager.autoFitContent = false;
            if (manager.padding == null)
            {
                manager.padding = new ButtonManager.Padding();
            }

            manager.padding.left = 0;
            manager.padding.right = 0;
            manager.padding.top = 0;
            manager.padding.bottom = 0;
            manager.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                manager.onClick.AddListener(onClick);
            }

            manager.UpdateUI();
            ApplyHeatButtonPalette(instance, sprite);

            button = instance.GetComponent<Button>() ?? instance.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            button.interactable = true;

            var raycastImage = instance.GetComponent<Image>() ?? instance.AddComponent<Image>();
            raycastImage.color = TransparentRaycastColor();
            raycastImage.raycastTarget = true;

            if (!string.IsNullOrEmpty(label))
            {
                var labelText = CreateText(instance.transform, $"{name}_Label", label, FontSizeForButton(label), FontStyle.Bold, labelColor, TextAnchor.MiddleCenter);
                labelText.raycastTarget = false;
                IgnoreLayout(labelText.gameObject);
                Stretch(labelText.rectTransform, 22, 10, -22, -10);
                labelText.transform.SetAsLastSibling();
            }

            return true;
        }

        private bool TryAddHeatInputPrefabVisual(Transform parent)
        {
            if (theme.HeatInputFieldPrefab == null)
            {
                return false;
            }

            var instance = UnityEngine.Object.Instantiate(theme.HeatInputFieldPrefab, parent, false);
            instance.name = "HeatInputFieldPrefabVisual";
            var rect = instance.GetComponent<RectTransform>() ?? instance.AddComponent<RectTransform>();
            Stretch(rect, 0, 0, 0, 0);
            instance.transform.SetAsFirstSibling();

            var layout = instance.GetComponent<LayoutElement>() ?? instance.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            DisableHeatAutoSizing(instance);
            DisableTmpText(instance.transform);
            DisableRaycasts(instance.transform);
            FreezeHeatUiManagerImages(instance);
            ApplyHeatInputPalette(instance);

            foreach (var manager in instance.GetComponentsInChildren<InputFieldManager>(true))
            {
                manager.enabled = false;
            }

            foreach (var input in instance.GetComponentsInChildren<TMP_InputField>(true))
            {
                input.interactable = false;
                input.enabled = false;
            }

            return true;
        }

        private bool TryCreateHeatPrefabProgressBar(Transform parent, string name, float value01, bool magenta, out RectTransform rect)
        {
            rect = null;
            if (theme.HeatProgressBarPrefab == null)
            {
                return false;
            }

            var instance = UnityEngine.Object.Instantiate(theme.HeatProgressBarPrefab, parent, false);
            instance.name = name;
            rect = instance.GetComponent<RectTransform>() ?? instance.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;

            DisableHeatAutoSizing(instance);
            DisableTmpText(instance.transform);
            DisableRaycasts(instance.transform);
            FreezeHeatUiManagerImages(instance);

            var progress = instance.GetComponent<ProgressBar>() ?? instance.GetComponentInChildren<ProgressBar>(true);
            if (progress == null)
            {
                UnityEngine.Object.Destroy(instance);
                rect = null;
                return false;
            }

            ApplyHeatProgressPalette(instance, progress.barImage, magenta);
            progress.minValue = 0f;
            progress.maxValue = 100f;
            progress.addPrefix = false;
            progress.addSuffix = false;
            progress.barDirection = ProgressBar.BarDirection.Left;
            if (progress.barImage != null)
            {
                progress.barImage.color = magenta ? theme.Magenta : theme.Cyan;
                progress.barImage.type = Image.Type.Filled;
                progress.barImage.fillMethod = Image.FillMethod.Horizontal;
                progress.barImage.fillOrigin = 0;
            }

            progress.SetValue(Mathf.Clamp01(value01) * 100f);
            return true;
        }

        private void ApplyHeatButtonPalette(GameObject instance, Sprite sprite)
        {
            var resolvedSprite = sprite != null ? sprite : theme.PrimaryButton;
            var normalColor = ButtonColor(resolvedSprite);
            var highlightColor = HighlightColor(normalColor);
            var disabledColor = new Color(0.075f, 0.085f, 0.13f, 0.88f);
            var frameColor = WithAlpha(resolvedSprite == theme.DangerButton ? theme.Magenta : theme.Cyan, 0.92f);

            foreach (var image in instance.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject == instance)
                {
                    image.color = TransparentRaycastColor();
                    image.raycastTarget = true;
                    continue;
                }

                image.raycastTarget = false;
                if (NameContains(image.transform, "Frame"))
                {
                    image.color = frameColor;
                }
                else if (NameContains(image.transform, "Shadow"))
                {
                    image.color = new Color(0f, 0f, 0f, 0.46f);
                }
                else if (HasAncestorNamed(image.transform, "Disabled"))
                {
                    image.color = disabledColor;
                }
                else if (HasAncestorNamed(image.transform, "Highlighted"))
                {
                    image.color = WithAlpha(highlightColor, 0.98f);
                }
                else if (HasAncestorNamed(image.transform, "Normal"))
                {
                    image.color = WithAlpha(normalColor, 0.96f);
                }
                else if (NameContains(image.transform, "Background"))
                {
                    image.color = WithAlpha(normalColor, 0.86f);
                }
                else if (NameContains(image.transform, "Static"))
                {
                    image.color = HeatSurfaceColor(0.42f);
                }
            }
        }

        private void ApplyHeatInputPalette(GameObject instance)
        {
            foreach (var image in instance.GetComponentsInChildren<Image>(true))
            {
                image.raycastTarget = false;
                if (NameContains(image.transform, "Frame"))
                {
                    image.color = WithAlpha(theme.Cyan, 0.9f);
                }
                else if (NameContains(image.transform, "Highlight"))
                {
                    image.color = HeatSurfaceColor(0.94f);
                }
                else if (NameContains(image.transform, "Shadow"))
                {
                    image.color = new Color(0f, 0f, 0f, 0.5f);
                }
                else if (NameContains(image.transform, "Static") || NameContains(image.transform, "Filler"))
                {
                    image.color = HeatSurfaceColor(0.9f);
                }
                else
                {
                    image.color = HeatSurfaceColor(Mathf.Min(image.color.a, 0.72f));
                }
            }
        }

        private void ApplyHeatProgressPalette(GameObject instance, Image barImage, bool magenta)
        {
            var fillColor = magenta ? theme.Magenta : theme.Cyan;
            foreach (var image in instance.GetComponentsInChildren<Image>(true))
            {
                image.raycastTarget = false;
                if (image == barImage || NameContains(image.transform, "Bar Image"))
                {
                    image.color = WithAlpha(fillColor, 0.96f);
                }
                else if (NameContains(image.transform, "Frame"))
                {
                    image.color = WithAlpha(theme.Cyan, 0.78f);
                }
                else if (NameContains(image.transform, "Background") || NameContains(image.transform, "Bar"))
                {
                    image.color = HeatSurfaceColor(0.9f);
                }
                else
                {
                    image.color = WithAlpha(theme.MutedText, 0.78f);
                }
            }
        }

        private void ConfigureHeatButton(Button button, string name, Sprite sprite)
        {
            var rootImage = button.GetComponent<Image>();
            var resolvedSprite = sprite != null ? sprite : theme.PrimaryButton;
            var normalColor = ButtonColor(resolvedSprite);
            var highlightColor = HighlightColor(normalColor);
            var disabledColor = new Color(0.12f, 0.13f, 0.18f, 0.84f);
            rootImage.color = new Color(0f, 0f, 0f, 0.01f);

            var normal = CreateHeatButtonState(button.transform, $"{name}_HeatNormal", resolvedSprite, normalColor, 1f);
            var highlight = CreateHeatButtonState(button.transform, $"{name}_HeatHighlight", resolvedSprite, highlightColor, 0f);
            var disabled = CreateHeatButtonState(button.transform, $"{name}_HeatDisabled", resolvedSprite, disabledColor, 0f);
            AddHeatAccentLine(normal.Group.transform, $"{name}_HeatNormalAccent", theme.Cyan, false);
            AddHeatAccentLine(highlight.Group.transform, $"{name}_HeatHighlightAccent", theme.Magenta, false);

            var manager = button.gameObject.GetComponent<ButtonManager>() ?? button.gameObject.AddComponent<ButtonManager>();
            manager.buttonText = string.Empty;
            manager.enableText = false;
            manager.enableIcon = false;
            manager.useLocalization = false;
            manager.useSounds = false;
            manager.checkForDoubleClick = false;
            manager.autoFitContent = false;
            manager.normalTextObj = null;
            manager.highlightTextObj = null;
            manager.disabledTextObj = null;
            SetPrivateField(manager, "normalCG", normal.Group);
            SetPrivateField(manager, "highlightCG", highlight.Group);
            SetPrivateField(manager, "disabledCG", disabled.Group);
            manager.UpdateUI();
        }

        private HeatButtonState CreateHeatButtonState(Transform parent, string name, Sprite sprite, Color frameColor, float alpha)
        {
            var state = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            state.transform.SetParent(parent, false);
            var rect = state.GetComponent<RectTransform>();
            Stretch(rect, 0, 0, 0, 0);
            var image = state.GetComponent<Image>();
            ApplySprite(image, sprite);
            image.color = frameColor;
            image.raycastTarget = false;

            var group = state.GetComponent<CanvasGroup>();
            group.alpha = alpha;

            return new HeatButtonState(group);
        }

        private void AddHeatPanelFill(RectTransform panel, Sprite panelSprite)
        {
            AddHeatInsetFill(panel, "HeatPanelFill", PanelColor(panelSprite), panelSprite == theme.StatCard ? 9f : 12f);
            if (panelSprite != theme.StatCard)
            {
                AddHeatAccentLine(panel, "HeatPanelTopAccent", PanelAccentColor(panelSprite), false);
            }
        }

        private void AddHeatInsetFill(Transform parent, string name, Color color, float inset)
        {
            var fill = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            fill.transform.SetParent(parent, false);
            var rect = fill.GetComponent<RectTransform>();
            Stretch(rect, inset, inset, -inset, -inset);
            fill.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = fill.GetComponent<Image>();
            ApplySprite(image, theme.StatCard != null ? theme.StatCard : theme.PrimaryButton);
            image.color = color;
            image.raycastTarget = false;
            fill.transform.SetAsFirstSibling();
        }

        private void AddHeatAccentLine(Transform parent, string name, Color color, bool bottom)
        {
            var accent = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accent.transform.SetParent(parent, false);
            accent.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = accent.GetComponent<RectTransform>();
            rect.anchorMin = bottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            rect.anchorMax = bottom ? new Vector2(1f, 0f) : new Vector2(1f, 1f);
            rect.offsetMin = bottom ? new Vector2(16f, 6f) : new Vector2(16f, -9f);
            rect.offsetMax = bottom ? new Vector2(-16f, 9f) : new Vector2(-16f, -6f);
            var image = accent.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static void DisableHeatAutoSizing(GameObject instance)
        {
            foreach (var fitter in instance.GetComponentsInChildren<ContentSizeFitter>(true))
            {
                fitter.enabled = false;
            }
        }

        private static void DisableTmpText(Transform parent)
        {
            foreach (var text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.gameObject.SetActive(false);
            }
        }

        private static void DisableRaycasts(Transform parent)
        {
            foreach (var graphic in parent.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static void FreezeHeatUiManagerImages(GameObject instance)
        {
            foreach (var manager in instance.GetComponentsInChildren<UIManagerImage>(true))
            {
                manager.useCustomColor = true;
                manager.enabled = false;
            }
        }

        private static void IgnoreLayout(GameObject target)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        private static bool NameContains(Transform transform, string value)
        {
            return transform.name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasAncestorNamed(Transform transform, string value)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (NameContains(current, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static Color TransparentRaycastColor()
        {
            return new Color(0f, 0f, 0f, 0.01f);
        }

        private readonly struct HeatButtonState
        {
            public HeatButtonState(CanvasGroup group)
            {
                Group = group;
            }

            public CanvasGroup Group { get; }
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
                return new Color(0.055f, 0.095f, 0.15f, 0.9f);
            }

            if (sprite == theme.StatCard)
            {
                return new Color(0.025f, 0.055f, 0.11f, 0.84f);
            }

            if (sprite == theme.LogPanel)
            {
                return new Color(0.015f, 0.032f, 0.08f, 0.82f);
            }

            return new Color(0.02f, 0.05f, 0.12f, 0.82f);
        }

        private Color HeatSurfaceColor(float alpha)
        {
            return new Color(0.012f, 0.028f, 0.072f, alpha);
        }

        private Color InputPlaceholderColor()
        {
            return theme.UseHeatUiSkin ? new Color(0.72f, 0.82f, 0.98f, 0.9f) : theme.MutedText;
        }

        private Color PanelFrameColor(Sprite sprite)
        {
            if (sprite == theme.RaidPanel)
            {
                return new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.68f);
            }

            if (sprite == theme.LogPanel)
            {
                return new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.26f);
            }

            if (sprite == theme.NotificationPanel)
            {
                return new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.58f);
            }

            if (sprite == theme.StatCard)
            {
                return new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.16f);
            }

            return new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.38f);
        }

        private Color PanelAccentColor(Sprite sprite)
        {
            if (sprite == theme.NotificationPanel)
            {
                return theme.Cyan;
            }

            if (sprite == theme.StatCard)
            {
                return new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.72f);
            }

            return theme.Cyan;
        }

        private Color ButtonColor(Sprite sprite)
        {
            if (!theme.UseHeatUiSkin)
            {
                return Color.white;
            }

            if (sprite == theme.DangerButton)
            {
                return new Color(0.08f, 0.16f, 0.38f, 0.96f);
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
                return 20;
            }

            if (label.Length >= 11)
            {
                return 16;
            }

            return label.Length >= 7 ? 18 : 20;
        }
    }
}
