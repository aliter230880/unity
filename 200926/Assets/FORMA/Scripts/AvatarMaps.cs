using UnityEngine;

namespace Forma
{
    public class PixelMap
    {
        public Color[] Data;
        public int Width, Height;
        public float MinX, MinY, MaxX, MaxY;

        public Color Sample(float u, float v)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
            float x = MinX + u * (MaxX - MinX);
            float y = MinY + (1f - v) * (MaxY - MinY);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, Height - 1);
            int x1 = Mathf.Min(Width - 1, x0 + 1);
            int y1 = Mathf.Min(Height - 1, y0 + 1);
            float tx = x - x0;
            float ty = y - y0;
            Color a = Data[y0 * Width + x0];
            Color b = Data[y0 * Width + x1];
            Color c = Data[y1 * Width + x0];
            Color e = Data[y1 * Width + x1];
            return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, e, tx), ty);
        }
    }

    public class AvatarMaps
    {
        public PixelMap FemaleFront, FemaleBack, FemaleFace;
        public PixelMap MaleFront, MaleBack, MaleFace;
        public Texture2D HairBlonde, HairShort, Beard;
    }

    public static class AvatarMapsLoader
    {
        public static AvatarMaps Load()
        {
            var maps = new AvatarMaps();
            maps.FemaleFront = Cutout(LoadTex("FORMA/female-front"), true);
            maps.FemaleBack = Cutout(LoadTex("FORMA/female-back"), true);
            maps.FemaleFace = Cutout(LoadTex("FORMA/female-face"), true);
            maps.MaleFront = Cutout(LoadTex("FORMA/male-front"), true);
            maps.MaleBack = Cutout(LoadTex("FORMA/male-back"), true);
            maps.MaleFace = Cutout(LoadTex("FORMA/male-face"), true);
            maps.HairBlonde = HairTex(LoadTex("FORMA/hair-blonde"), false);
            maps.HairShort = HairTex(LoadTex("FORMA/hair-short"), false);
            maps.Beard = HairTex(LoadTex("FORMA/beard"), true);
            return maps;
        }

        static Texture2D LoadTex(string path)
        {
            var t = Resources.Load<Texture2D>(path);
            if (t == null)
            {
                // Also try without folder prefix for loose imports
                t = Resources.Load<Texture2D>(path.Replace("FORMA/", ""));
            }
            return t;
        }

        static PixelMap Cutout(Texture2D tex, bool lightMode)
        {
            if (tex == null) return null;
            Texture2D readable = tex;
            if (!tex.isReadable)
            {
                try
                {
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }
                catch
                {
                    return null;
                }
            }
            int w = readable.width, h = readable.height;
            Color[] d;
            try { d = readable.GetPixels(); }
            catch { return null; }
            Color bg = (d[0] + d[w - 1] + d[(h - 1) * w] + d[(h - 1) * w + (w - 1)]) * 0.25f;
            float thresh = lightMode ? 42f / 255f : 28f / 255f;
            int minX = w, minY = h, maxX = 0, maxY = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = d[i];
                    float dist = Vector3.Distance(new Vector3(c.r, c.g, c.b), new Vector3(bg.r, bg.g, bg.b));
                    float lum = (c.r + c.g + c.b) / 3f;
                    float a = c.a;
                    if (lightMode)
                    {
                        if (dist < thresh || lum > 246f / 255f) a = 0;
                        else if (dist < thresh * 2.2f) a = (dist - thresh) / thresh;
                        else a = 1f;
                    }
                    else
                    {
                        if (lum < 12f / 255f) a = 0;
                        else if (lum < 40f / 255f) a = (lum - 12f / 255f) / (28f / 255f);
                        else a = 1f;
                    }
                    d[i] = new Color(c.r, c.g, c.b, a);
                    if (a > 40f / 255f)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX <= minX) { minX = 0; minY = 0; maxX = w - 1; maxY = h - 1; }
            float padX = (maxX - minX) * 0.04f;
            float padY = (maxY - minY) * 0.02f;
            return new PixelMap
            {
                Data = d, Width = w, Height = h,
                MinX = Mathf.Max(0, minX - padX),
                MinY = Mathf.Max(0, minY - padY),
                MaxX = Mathf.Min(w - 1, maxX + padX),
                MaxY = Mathf.Min(h - 1, maxY + padY)
            };
        }

        static Texture2D HairTex(Texture2D src, bool invert)
        {
            var map = Cutout(src, invert);
            if (map == null) return src;
            var t = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false);
            t.SetPixels(map.Data);
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }
    }
}
