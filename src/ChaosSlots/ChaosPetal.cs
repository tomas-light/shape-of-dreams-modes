using UnityEngine;

namespace ChaosSlots
{
    internal static class ChaosPetal
    {
        private static Sprite _sprite;
        private static Texture2D _texture;

        internal static Sprite Sprite
        {
            get
            {
                if (_sprite == null)
                    Create();
                return _sprite;
            }
        }

        private static void Create()
        {
            const int width = 48;
            const int height = 64;
            var rim = new Color(1f, 0.69f, 0.81f);
            var shadow = new Color(0.27f, 0.06f, 0.31f);
            var crimson = new Color(0.89f, 0.20f, 0.47f);
            var colors = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (y + 0.5f) / height;
                float arc = Mathf.Clamp01((t - 0.04f) / 0.92f);
                float center = 0.38f + 0.28f * arc * arc;
                float halfWidth = 0.36f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(arc * Mathf.PI)), 0.87f);
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float edge = halfWidth - Mathf.Abs(u - center);
                    float alpha = Mathf.Clamp01(edge * width + 0.5f);
                    var fill = Color.Lerp(shadow, crimson, Mathf.Clamp01(arc * 0.8f + (center - u) * 0.8f));
                    float vein = Mathf.Clamp01(1f - Mathf.Abs(u - center + 0.02f) * 40f) * Mathf.Sin(arc * Mathf.PI);
                    fill = Color.Lerp(fill, rim, vein * 0.65f);
                    var pixel = Color.Lerp(rim, fill, Mathf.Clamp01(edge * 15f));
                    pixel.a = alpha;
                    colors[y * width + x] = pixel;
                }
            }

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Chaos slot petal",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _texture.SetPixels32(colors);
            _texture.Apply(false, true);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        }

        internal static void Release()
        {
            Object.Destroy(_sprite);
            Object.Destroy(_texture);
            _sprite = null;
            _texture = null;
        }
    }
}
