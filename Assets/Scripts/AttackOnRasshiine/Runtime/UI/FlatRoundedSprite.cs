using System.Collections.Generic;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.UI
{
    /// <summary>
    /// Generates simple, un-ornamented rounded-rectangle sprites at runtime so flat
    /// low-poly-consistent UI (login panel/inputs/buttons, and eventually the rest of
    /// the app) reads as designed geometry instead of a sharp-cornered placeholder
    /// rectangle. No texture asset dependency: a single small white 9-sliced texture
    /// per corner radius, tinted through Image.color.
    /// </summary>
    public static class FlatRoundedSprite
    {
        private const int TextureSize = 64;
        private static readonly Dictionary<int, Sprite> Cache = new();

        public static Sprite Get(int cornerRadiusPixels)
        {
            cornerRadiusPixels = Mathf.Clamp(cornerRadiusPixels, 2, TextureSize / 2 - 1);
            if (Cache.TryGetValue(cornerRadiusPixels, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"FlatRoundedSprite_{cornerRadiusPixels}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[TextureSize * TextureSize];
            var radius = (float)cornerRadiusPixels;
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var alpha = RoundedRectCoverage(x, y, TextureSize, radius);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var border = cornerRadiusPixels + 1f;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = $"FlatRoundedSprite_{cornerRadiusPixels}";
            sprite.hideFlags = HideFlags.DontSave;

            Cache[cornerRadiusPixels] = sprite;
            return sprite;
        }

        private static float RoundedRectCoverage(int x, int y, int size, float radius)
        {
            // Sample the pixel center; distance from the nearest rounded corner arc
            // center determines a 1px-ish antialiased edge via smoothstep.
            var px = x + 0.5f;
            var py = y + 0.5f;
            var minX = radius;
            var minY = radius;
            var maxX = size - radius;
            var maxY = size - radius;

            var cx = Mathf.Clamp(px, minX, maxX);
            var cy = Mathf.Clamp(py, minY, maxY);
            var dx = px - cx;
            var dy = py - cy;
            var distance = Mathf.Sqrt(dx * dx + dy * dy);

            var edge = radius - distance;
            return Mathf.Clamp01(edge + 0.5f);
        }
    }
}
