using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forma
{
    public static class HairBuilder
    {
        struct StyleCfg
        {
            public int count, segs;
            public float length, gravity, curl, minY, radius;
        }

        static StyleCfg Config(HairStyle style, float lenMul, float vol)
        {
            switch (style)
            {
                case HairStyle.Short: return new StyleCfg { count = 280, segs = 4, length = 0.045f * lenMul, gravity = 0.28f, curl = 0.004f, minY = -0.05f, radius = 0.0022f * vol };
                case HairStyle.Buzz: return new StyleCfg { count = 320, segs = 3, length = 0.022f * lenMul, gravity = 0.1f, curl = 0.002f, minY = -0.12f, radius = 0.0018f * vol };
                case HairStyle.Pixie: return new StyleCfg { count = 340, segs = 5, length = 0.075f * lenMul, gravity = 0.5f, curl = 0.01f, minY = -0.22f, radius = 0.0038f * vol };
                case HairStyle.Sidepart: return new StyleCfg { count = 320, segs = 6, length = 0.1f * lenMul, gravity = 0.7f, curl = 0.008f, minY = -0.06f, radius = 0.004f * vol };
                case HairStyle.Bob: return new StyleCfg { count = 340, segs = 7, length = 0.17f * lenMul, gravity = 1.1f, curl = 0.012f, minY = -0.16f, radius = 0.0044f * vol };
                case HairStyle.Waves: return new StyleCfg { count = 260, segs = 12, length = 0.55f * lenMul, gravity = 1.7f, curl = 0.034f, minY = 0.05f, radius = 0.0018f * vol };
                case HairStyle.Long: return new StyleCfg { count = 240, segs = 14, length = 0.62f * lenMul, gravity = 1.8f, curl = 0.016f, minY = 0.08f, radius = 0.0017f * vol };
                case HairStyle.Ponytail: return new StyleCfg { count = 300, segs = 10, length = 0.26f * lenMul, gravity = 1.25f, curl = 0.01f, minY = 0f, radius = 0.0038f * vol };
                case HairStyle.Bun: return new StyleCfg { count = 260, segs = 6, length = 0.12f * lenMul, gravity = 0.3f, curl = 0.02f, minY = 0.05f, radius = 0.0044f * vol };
                case HairStyle.Afro: return new StyleCfg { count = 420, segs = 6, length = 0.15f * lenMul, gravity = 0.15f, curl = 0.048f, minY = -0.28f, radius = 0.0065f * vol };
                case HairStyle.Curls: return new StyleCfg { count = 320, segs = 10, length = 0.32f * lenMul, gravity = 1.15f, curl = 0.042f, minY = -0.08f, radius = 0.0046f * vol };
                default: return new StyleCfg { count = 220, segs = 5, length = 0.08f, gravity = 0.6f, curl = 0.01f, minY = 0, radius = 0.004f };
            }
        }

        static List<Vector3> Fibonacci(int n, float minY)
        {
            var pts = new List<Vector3>(n);
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < n; i++)
            {
                float y = 1f - (i / (float)(n - 1)) * 2f;
                if (y < minY) continue;
                float r = Mathf.Sqrt(1f - y * y);
                float a = golden * i;
                pts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
            return pts;
        }

        static List<Vector3> StrandCurve(Vector3 origin, Vector3 dir, float length, float gravity, float curl, float sideshift, int segs, Func<float> rand)
        {
            var pts = new List<Vector3>(segs + 1);
            dir.Normalize();
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = origin + dir * (length * t);
                p.y -= gravity * t * t * length;
                p.x += Mathf.Sin(t * 6f + rand() * 4f) * curl * t;
                p.z += Mathf.Cos(t * 5.2f) * curl * 0.6f * t;
                p.x += sideshift * t;
                pts.Add(p);
            }
            return pts;
        }

        public static Mesh StrandMesh(AvatarParams p)
        {
            if (p.hairStyle == HairStyle.None) return null;
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            var center = new Vector3(0, (p.sex == Sex.Female ? 1.64f : 1.72f) * h, 0.012f);
            float headR = 0.1f * MathUtil.Lerp(0.9f, 1.12f, p.headWidth);
            float lenMul = MathUtil.Lerp(0.7f, 1.4f, p.hairLength);
            float vol = MathUtil.Lerp(0.75f, 1.4f, p.hairVolume);
            var rand = MathUtil.Mulberry((int)p.hairStyle * 97 + Mathf.RoundToInt(p.hairLength * 20));
            var cfg = Config(p.hairStyle, lenMul, vol);
            var roots = Fibonacci(cfg.count, cfg.minY);
            var b = new MeshBuilder();
            const int radial = 5;
            var style = p.hairStyle;

            foreach (var nrm in roots)
            {
                var origin = center + nrm * (headR * 0.98f);
                if (nrm.z > 0.48f && nrm.y < 0.22f) continue;
                if (style == HairStyle.Ponytail && nrm.z > 0.15f && nrm.y < 0.55f) continue;
                if (style == HairStyle.Bun && nrm.y < 0.15f) continue;
                var dir = nrm;
                if (style == HairStyle.Sidepart) dir.x += 0.45f;
                if (style == HairStyle.Ponytail) dir = Vector3.Lerp(dir, new Vector3(0, 0.2f, -1), 0.35f);
                if (style == HairStyle.Waves || style == HairStyle.Long)
                {
                    dir.x += nrm.x > 0 ? 0.12f : -0.28f;
                    dir.z -= 0.12f;
                }
                float extra = style == HairStyle.Ponytail && nrm.z < -0.1f && nrm.y > 0.2f ? cfg.length * 1.45f : cfg.length;
                var curve = StrandCurve(origin, dir, extra * (0.75f + rand() * 0.35f), cfg.gravity, cfg.curl * (0.6f + rand() * 0.8f), style == HairStyle.Sidepart ? 0.04f : 0f, cfg.segs, rand);
                if (style == HairStyle.Bun)
                {
                    var bun = center + new Vector3(0, headR * 0.55f, -headR * 0.35f);
                    for (int i = 0; i < curve.Count; i++)
                    {
                        float t = i / (float)(curve.Count - 1);
                        curve[i] = Vector3.Lerp(curve[i], bun, Mathf.Pow(t, 1.4f));
                    }
                }
                int start = b.Positions.Count;
                for (int i = 0; i < curve.Count; i++)
                {
                    var p0 = curve[i];
                    var p1 = curve[Mathf.Min(i + 1, curve.Count - 1)];
                    var tng = p1 - p0;
                    if (tng.sqrMagnitude < 1e-8f) tng = new Vector3(0, -1, 0);
                    tng.Normalize();
                    var n = Vector3.Cross(new Vector3(0, 1, 0), tng);
                    if (n.sqrMagnitude < 1e-8f) n = new Vector3(1, 0, 0);
                    n.Normalize();
                    var bin = Vector3.Cross(tng, n).normalized;
                    float r = cfg.radius * (1f - (i / (float)(curve.Count - 1)) * 0.7f) * vol;
                    for (int k = 0; k < radial; k++)
                    {
                        float a = (k / (float)radial) * Mathf.PI * 2f;
                        var q = p0 + n * (Mathf.Cos(a) * r) + bin * (Mathf.Sin(a) * r);
                        b.Vert(q.x, q.y, q.z, k / (float)radial, i / (float)(curve.Count - 1), 0);
                    }
                }
                for (int i = 0; i < curve.Count - 1; i++)
                {
                    for (int k = 0; k < radial; k++)
                    {
                        int a = start + i * radial + k;
                        int c = start + i * radial + ((k + 1) % radial);
                        int d = start + (i + 1) * radial + k;
                        int e = start + (i + 1) * radial + ((k + 1) % radial);
                        b.Tri(a, d, c);
                        b.Tri(c, d, e);
                    }
                }
            }
            if (b.Positions.Count == 0) return null;
            return b.ToUnityMesh("FORMA.Hair");
        }

        public static Mesh BeardMesh(AvatarParams p)
        {
            if (p.facialHair < 0.04f || p.sex != Sex.Male) return null;
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            var center = new Vector3(0, (p.sex == Sex.Female ? 1.52f : 1.58f) * h, 0.04f);
            float amt = p.facialHair;
            var rand = MathUtil.Mulberry(44);
            var b = new MeshBuilder();
            const int radial = 4;
            int count = Mathf.RoundToInt(180 * amt);
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)count;
                float ang = MathUtil.Lerp(-1.15f, 1.15f, u);
                var origin = new Vector3(Mathf.Sin(ang) * 0.06f, center.y + Mathf.Exp(-ang * ang * 2.2f) * 0.02f, 0.055f + Mathf.Cos(ang) * 0.03f);
                var dir = new Vector3(Mathf.Sin(ang) * 0.3f, -0.7f, 0.25f);
                float len = MathUtil.Lerp(0.018f, 0.055f, amt) * (0.7f + rand() * 0.5f);
                var curve = StrandCurve(origin, dir, len, 0.4f, 0.006f, 0, 4, rand);
                int start = b.Positions.Count;
                for (int s = 0; s < curve.Count; s++)
                {
                    var p0 = curve[s];
                    float r = 0.0032f * (1f - s / (float)(curve.Count - 1));
                    for (int k = 0; k < radial; k++)
                    {
                        float a = (k / (float)radial) * Mathf.PI * 2f;
                        b.Vert(p0.x + Mathf.Cos(a) * r, p0.y, p0.z + Mathf.Sin(a) * r, 0, 0, 0);
                    }
                }
                for (int s = 0; s < curve.Count - 1; s++)
                {
                    for (int k = 0; k < radial; k++)
                    {
                        int a = start + s * radial + k;
                        int c = start + s * radial + ((k + 1) % radial);
                        int d = start + (s + 1) * radial + k;
                        int e = start + (s + 1) * radial + ((k + 1) % radial);
                        b.Tri(a, d, c);
                        b.Tri(c, d, e);
                    }
                }
            }
            if (b.Positions.Count == 0) return null;
            return b.ToUnityMesh("FORMA.Beard");
        }
    }
}
