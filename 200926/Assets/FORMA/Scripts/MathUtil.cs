using System;
using UnityEngine;

namespace Forma
{
    public static class MathUtil
    {
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float Clamp01(float v) => Mathf.Clamp01(v);

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static float Rbf(float dx, float dy, float dz, float radius)
        {
            float t = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / radius;
            if (t >= 1f) return 0f;
            float x = 1f - t * t;
            return x * x;
        }

        public static float Gauss(float x, float s)
        {
            return Mathf.Exp(-(x * x) / (2f * s * s));
        }

        public static float Kf(float[,] keys, float y)
        {
            int n = keys.GetLength(0);
            if (y <= keys[0, 0]) return keys[0, 1];
            float lastX = keys[n - 1, 0];
            float lastY = keys[n - 1, 1];
            if (y >= lastX) return lastY;
            for (int i = 0; i < n - 1; i++)
            {
                float ax = keys[i, 0], ay = keys[i, 1];
                float bx = keys[i + 1, 0], by = keys[i + 1, 1];
                if (y <= bx) return Lerp(ay, by, (y - ax) / (bx - ax));
            }
            return lastY;
        }

        public static float Off(float v) => (v - 0.5f) * 2f;

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            string h = hex.Replace("#", "").Trim();
            if (h.Length == 3)
                h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length < 6) h = h.PadRight(6, '0');
            if (!uint.TryParse(h.Substring(0, 6), System.Globalization.NumberStyles.HexNumber, null, out uint n))
                return Color.white;
            return new Color(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, 1f);
        }

        public static int Imul(int a, int b) => unchecked(a * b);

        public static Func<float> Mulberry(int seed)
        {
            int a = seed;
            return () =>
            {
                a = unchecked(a + unchecked((int)0x6D2B79F5));
                int t = Imul(a ^ (a >> 15), 1 | a);
                t = (t + Imul(t ^ (t >> 7), 61 | t)) ^ t;
                return ((uint)(t ^ (t >> 14))) / 4294967296f;
            };
        }
    }
}
