using UnityEngine;

namespace Forma
{
    public static class TextureFactory
    {
        public static Texture2D MakeIris(string hex, int size = 256)
        {
            var col = MathUtil.Hex(hex);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float cx = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cx) / cx;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    Color c;
                    if (r > 1f) c = Color.clear;
                    else if (r < 0.22f) c = new Color(0.04f, 0.03f, 0.03f, 1f);
                    else
                    {
                        float t = Mathf.InverseLerp(0.22f, 1f, r);
                        var ring = Color.Lerp(col * 1.15f, col * 0.35f, t);
                        float spoke = 0.12f * Mathf.Sin(a * 18f + r * 9f);
                        ring *= 1f + spoke;
                        if (r > 0.92f) ring = Color.Lerp(ring, new Color(0.08f, 0.06f, 0.05f, 1f), (r - 0.92f) / 0.08f);
                        c = ring;
                        c.a = 1f;
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static Texture2D MakeSkin(AvatarParams p, int size = 1024)
        {
            var skin = MathUtil.Hex(p.skin);
            var freckle = MathUtil.Hex(p.freckleColor);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float scale = MathUtil.Lerp(18f, 48f, p.freckleScale);
            float amt = p.freckles;
            var rand = MathUtil.Mulberry(91);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x / scale, y / scale);
                    float n2 = Mathf.PerlinNoise(x / (scale * 0.37f) + 8.2f, y / (scale * 0.37f));
                    float speckle = n * n2;
                    float mix = amt * Mathf.SmoothStep(0.55f, 0.85f, speckle);
                    var c = Color.Lerp(skin, freckle, mix * 0.55f);
                    float pore = Mathf.PerlinNoise(x / 3.7f, y / 3.7f) - 0.5f;
                    float grain = ((rand() - 0.5f) * 0.018f + pore * 0.012f) * (1f - p.smoothness * 0.72f);
                    c.r = Mathf.Clamp01(c.r + grain);
                    c.g = Mathf.Clamp01(c.g + grain * 0.8f);
                    c.b = Mathf.Clamp01(c.b + grain * 0.6f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static Texture2D MakeBump(AvatarParams p, int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float s = MathUtil.Lerp(24f, 8f, 1f - p.smoothness);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float macro = Mathf.PerlinNoise(x / s, y / s);
                    float pores = Mathf.PerlinNoise(x / 2.6f + 31.7f, y / 2.6f + 9.1f);
                    float n = Mathf.Clamp01(0.5f + (macro - 0.5f) * 0.2f + (pores - 0.5f) * 0.16f);
                    px[y * size + x] = new Color(n, n, 1f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
