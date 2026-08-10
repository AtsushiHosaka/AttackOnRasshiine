using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class BakedTextButtonImage : MonoBehaviour
    {
        private const int MinTextureWidth = 96;
        private const int MinTextureHeight = 32;
        private const int MaxTextureWidth = 1024;
        private const int MaxTextureHeight = 256;

        [SerializeField] private Sprite baseSprite;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Color surfaceTint = Color.white;
        [SerializeField] private int fontSize = 18;

        private Image targetImage;
        private Font font;
        private Texture2D bakedTexture;
        private Sprite bakedSprite;
        private Vector2Int lastTextureSize;
        private bool dirty = true;

        public Sprite BaseSprite => baseSprite;
        public string Label => label;

        public void Configure(Sprite sprite, string text, Font labelFont, int requestedFontSize, Color textColor, Color tint)
        {
            targetImage = GetComponent<Image>();
            baseSprite = sprite != null ? sprite : targetImage != null ? targetImage.sprite : null;
            label = text ?? string.Empty;
            font = labelFont;
            fontSize = Mathf.Max(10, requestedFontSize);
            labelColor = textColor;
            surfaceTint = tint;
            dirty = true;

            if (targetImage != null)
            {
                targetImage.sprite = baseSprite;
                targetImage.color = surfaceTint;
                targetImage.type = baseSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                targetImage.preserveAspect = false;
            }
        }

        private void OnEnable()
        {
            dirty = true;
        }

        private void OnDisable()
        {
            ReleaseBakedResources();
        }

        private void OnDestroy()
        {
            ReleaseBakedResources();
        }

        private void OnRectTransformDimensionsChange()
        {
            dirty = true;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BakeIfNeeded();
        }

        private void BakeIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            targetImage ??= GetComponent<Image>();
            if (targetImage == null)
            {
                return;
            }

            var rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            var rectSize = rect.rect.size;
            if (rectSize.x < 16f || rectSize.y < 12f)
            {
                return;
            }

            var textureSize = ResolveTextureSize(rectSize);
            if (!dirty && textureSize == lastTextureSize)
            {
                return;
            }

            ReleaseBakedResources();
            bakedTexture = ButtonSpriteBaker.Render(baseSprite, label, font, fontSize, labelColor, surfaceTint, textureSize.x, textureSize.y);
            if (bakedTexture == null)
            {
                return;
            }

            bakedSprite = Sprite.Create(
                bakedTexture,
                new Rect(0f, 0f, bakedTexture.width, bakedTexture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            bakedSprite.name = $"{name}_BakedSprite";

            targetImage.sprite = bakedSprite;
            targetImage.color = Color.white;
            targetImage.type = Image.Type.Simple;
            targetImage.preserveAspect = false;
            lastTextureSize = textureSize;
            dirty = false;
        }

        private static Vector2Int ResolveTextureSize(Vector2 rectSize)
        {
            var scale = rectSize.y < 42f ? 2.8f : 2.15f;
            var width = Mathf.Clamp(Mathf.RoundToInt(rectSize.x * scale), MinTextureWidth, MaxTextureWidth);
            var height = Mathf.Clamp(Mathf.RoundToInt(rectSize.y * scale), MinTextureHeight, MaxTextureHeight);
            return new Vector2Int(width, height);
        }

        private void ReleaseBakedResources()
        {
            if (bakedSprite != null)
            {
                DestroyRuntimeObject(bakedSprite);
                bakedSprite = null;
            }

            if (bakedTexture != null)
            {
                DestroyRuntimeObject(bakedTexture);
                bakedTexture = null;
            }

            lastTextureSize = Vector2Int.zero;
            dirty = true;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static class ButtonSpriteBaker
        {
            public static Texture2D Render(Sprite sprite, string text, Font font, int fontSize, Color textColor, Color tint, int width, int height)
            {
                var cameraObject = new GameObject("RasshiineButtonBakeCamera", typeof(Camera));
                var canvasObject = new GameObject("RasshiineButtonBakeCanvas", typeof(Canvas));
                var previousActive = RenderTexture.active;
                RenderTexture renderTexture = null;
                try
                {
                    cameraObject.hideFlags = HideFlags.HideAndDontSave;
                    canvasObject.hideFlags = HideFlags.HideAndDontSave;

                    var camera = cameraObject.GetComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                    camera.orthographic = true;
                    camera.orthographicSize = height * 0.5f;
                    camera.aspect = (float)width / height;
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = 20f;
                    camera.transform.position = new Vector3(0f, 0f, -10f);
                    camera.transform.rotation = Quaternion.identity;

                    var canvas = canvasObject.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                    canvas.pixelPerfect = false;

                    var canvasRect = canvas.GetComponent<RectTransform>();
                    canvasRect.sizeDelta = new Vector2(width, height);
                    canvasRect.position = Vector3.zero;
                    canvasRect.localScale = Vector3.one;

                    AddBackground(canvasObject.transform, sprite, tint, width, height);
                    AddLabel(canvasObject.transform, text, font, fontSize, textColor, width, height);

                    renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                    renderTexture.antiAliasing = 1;
                    camera.targetTexture = renderTexture;
                    RenderTexture.active = renderTexture;
                    GL.Clear(true, true, Color.clear);
                    camera.Render();

                    var texture = new Texture2D(width, height, TextureFormat.ARGB32, false)
                    {
                        name = $"BakedButton_{SanitizeName(text)}"
                    };
                    texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    // The pixels are immutable after baking. Discard the CPU copy
                    // once they have reached the GPU; retaining it doubles every
                    // baked button's persistent texture memory in WebGL.
                    texture.Apply(false, true);
                    return texture;
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    if (renderTexture != null)
                    {
                        RenderTexture.ReleaseTemporary(renderTexture);
                    }

                    DestroyRuntimeObject(canvasObject);
                    DestroyRuntimeObject(cameraObject);
                }
            }

            private static void AddBackground(Transform parent, Sprite sprite, Color tint, int width, int height)
            {
                var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backgroundObject.transform.SetParent(parent, false);
                var rect = backgroundObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = Vector2.zero;

                var image = backgroundObject.GetComponent<Image>();
                image.sprite = sprite;
                image.color = tint;
                image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                image.raycastTarget = false;
            }

            private static void AddLabel(Transform parent, string text, Font font, int requestedFontSize, Color textColor, int width, int height)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(parent, false);
                var rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                var horizontalPadding = Mathf.Clamp(width * 0.13f, 18f, 72f);
                var verticalPadding = Mathf.Clamp(height * 0.16f, 6f, 24f);
                rect.sizeDelta = new Vector2(Mathf.Max(12f, width - horizontalPadding * 2f), Mathf.Max(12f, height - verticalPadding * 2f));
                rect.anchoredPosition = Vector2.zero;

                var label = labelObject.GetComponent<Text>();
                label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.text = text;
                label.color = textColor;
                label.alignment = TextAnchor.MiddleCenter;
                label.alignByGeometry = true;
                label.fontStyle = FontStyle.Bold;
                label.supportRichText = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(height * 0.20f));
                label.resizeTextMaxSize = ResolveBakedFontSize(text, requestedFontSize, width, height);
                label.raycastTarget = false;
            }

            private static int ResolveBakedFontSize(string text, int requestedFontSize, int width, int height)
            {
                var baseSize = Mathf.Max(requestedFontSize, Mathf.RoundToInt(height * 0.34f));
                if (!string.IsNullOrEmpty(text) && text.Length >= 9)
                {
                    baseSize = Mathf.RoundToInt(baseSize * 0.86f);
                }

                if (!string.IsNullOrEmpty(text) && text.Length >= 13)
                {
                    baseSize = Mathf.RoundToInt(baseSize * 0.78f);
                }

                return Mathf.Clamp(baseSize, 12, Mathf.RoundToInt(height * 0.58f));
            }

            private static string SanitizeName(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return "Empty";
                }

                var result = text.Replace("/", "_").Replace("\\", "_").Replace(" ", string.Empty);
                return result.Length > 24 ? result.Substring(0, 24) : result;
            }
        }
    }
}
