using System.Collections.Generic;
using UnityEngine;

namespace Forma
{
    public static class GarmentBuilder
    {
        static void BodyScale(AvatarParams p, out float h, out float g, out float fem)
        {
            h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            g = MathUtil.Lerp(0.86f, 1.18f, p.build) * (1f + p.fat * 0.1f);
            fem = p.sex == Sex.Female ? 1f : 0f;
        }

        static Mesh BuildSleeve(AvatarParams p, float side)
        {
            BodyScale(p, out float h, out float g, out _);
            float shY = (p.sex == Sex.Female ? 1.34f : 1.42f) * h;
            float shX = (p.sex == Sex.Female ? 0.17f : 0.22f) * MathUtil.Lerp(0.88f, 1.28f, p.shoulders) * g;
            const int segs = 12;
            const int n = 8;
            var rings = new List<float[]>(n);
            float len = p.upper == UpperGarment.Shirt ? 0.38f : 0.16f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float x = side * (shX + 0.02f);
                float y = shY - t * len;
                float z = 0.03f;
                float r = MathUtil.Lerp(0.052f, 0.04f, t) * g * MathUtil.Lerp(0.8f, 1.3f, p.arms);
                var pts = new float[(segs + 1) * 3];
                for (int j = 0; j <= segs; j++)
                {
                    float a = (j / (float)segs) * Mathf.PI * 2f;
                    int k = j * 3;
                    pts[k] = x + Mathf.Cos(a) * r * 0.55f;
                    pts[k + 1] = y + Mathf.Sin(a) * r * 0.2f;
                    pts[k + 2] = z + Mathf.Sin(a) * r;
                }
                rings.Add(pts);
            }
            return MeshBuilder.Loft(rings, segs);
        }

        static Mesh HalterStraps(AvatarParams p)
        {
            BodyScale(p, out float h, out float g, out float fem);
            float chest = MathUtil.Lerp(0.74f, 1.48f, p.chest);
            float yChest = 1.26f * h;
            float yNeck = 1.5f * h;
            float rx = 0.07f * g;
            Mesh a = null, b = null;
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new[]
                {
                    new Vector3(s * rx * 1.4f, yChest, 0.08f + fem * chest * 0.02f),
                    new Vector3(s * rx * 0.7f, MathUtil.Lerp(yChest, yNeck, 0.55f), 0.04f),
                    new Vector3(s * 0.02f, yNeck + 0.01f, -0.01f)
                };
                var m = MeshBuilder.TubeFromPoints(pts, 0.007f * g);
                if (s < 0) a = m; else b = m;
            }
            return MeshBuilder.Merge(a, b);
        }

        public static Mesh BuildUpper(AvatarParams p)
        {
            if (p.full != FullGarment.None || p.upper == UpperGarment.None) return null;
            BodyScale(p, out float h, out float g, out float fem);
            const int segs = 32;
            float chest = MathUtil.Lerp(0.74f, 1.48f, p.chest);
            float waist = MathUtil.Lerp(0.72f, 1.3f, p.waist);
            float sh = MathUtil.Lerp(0.88f, 1.28f, p.shoulders);
            float y1 = (p.upper == UpperGarment.Crop ? 1.1f : p.upper == UpperGarment.Bandeau ? 1.16f : 0.98f) * h;
            float y0 = (p.upper == UpperGarment.Bandeau ? 1.28f : 1.38f) * h;
            const int n = 12;
            var rings = new List<float[]>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float y = MathUtil.Lerp(y0, y1, t);
                float k = MathUtil.Lerp(1.04f * sh, 0.96f * waist, t);
                float rx = (0.155f + fem * 0.012f) * g * k + (p.upper == UpperGarment.Shirt ? 0.018f : 0.012f);
                float rz = (0.125f + chest * 0.035f + fem * 0.03f) * g + 0.012f;
                rings.Add(MeshBuilder.Ring(y, rx, rz, segs, fem * chest * 0.03f));
            }
            var body = MeshBuilder.Loft(rings, segs);
            var parts = new List<Mesh> { body };
            if (p.upper == UpperGarment.Bandeau) parts.Add(HalterStraps(p));
            if (p.upper == UpperGarment.Tee || p.upper == UpperGarment.Shirt || p.upper == UpperGarment.Tank)
            {
                parts.Add(BuildSleeve(p, 1));
                parts.Add(BuildSleeve(p, -1));
            }
            return MeshBuilder.Merge(parts.ToArray());
        }

        static List<float[]> HipRings(float y0, float y1, int segs, float rx, float fem)
        {
            const int n = 8;
            var rings = new List<float[]>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                rings.Add(MeshBuilder.Ring(MathUtil.Lerp(y0, y1, t), rx * 0.168f, rx * 0.138f + fem * 0.012f, segs, -0.018f * fem));
            }
            return rings;
        }

        static Mesh LegGarment(AvatarParams p, float side, float yBot)
        {
            BodyScale(p, out float h, out float g, out _);
            const int segs = 16;
            const int n = 8;
            var rings = new List<float[]>(n);
            float hipY = 0.86f * h;
            float hipX = (p.sex == Sex.Female ? 0.09f : 0.085f) * MathUtil.Lerp(0.8f, 1.34f, p.hips) * g;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float y = MathUtil.Lerp(hipY, yBot, t);
                float x = side * MathUtil.Lerp(hipX, 0.045f, t);
                float r = MathUtil.Lerp(0.08f, p.lower == LowerGarment.Shorts ? 0.065f : 0.045f, t) * g * MathUtil.Lerp(0.8f, 1.32f, p.thighs);
                var pts = new float[(segs + 1) * 3];
                for (int j = 0; j <= segs; j++)
                {
                    float a = (j / (float)segs) * Mathf.PI * 2f;
                    int k = j * 3;
                    pts[k] = x + Mathf.Sin(a) * r;
                    pts[k + 1] = y;
                    pts[k + 2] = Mathf.Cos(a) * r * 0.9f;
                }
                rings.Add(pts);
            }
            return MeshBuilder.Loft(rings, segs);
        }

        public static Mesh BuildLower(AvatarParams p)
        {
            if (p.full != FullGarment.None || p.lower == LowerGarment.None) return null;
            BodyScale(p, out float h, out float g, out float fem);
            const int segs = 28;
            float hip = MathUtil.Lerp(0.8f, 1.34f, p.hips);
            float yTop = 0.97f * h;
            float yBot = p.lower == LowerGarment.Briefs ? 0.8f * h
                : p.lower == LowerGarment.Shorts ? 0.56f * h
                : p.lower == LowerGarment.Skirt ? 0.48f * h
                : 0.1f * h;
            if (p.lower == LowerGarment.Skirt)
            {
                const int n = 8;
                var rings = new List<float[]>(n);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    float y = MathUtil.Lerp(yTop, yBot, t);
                    float flare = 1f + t * 0.55f;
                    rings.Add(MeshBuilder.Ring(y, 0.165f * g * hip * flare, 0.14f * g * flare, segs, -0.01f));
                }
                return MeshBuilder.Loft(rings, segs);
            }
            var geos = new List<Mesh> { MeshBuilder.Loft(HipRings(yTop, Mathf.Max(yBot, 0.78f * h), segs, g * hip, fem), segs) };
            if (p.lower != LowerGarment.Briefs)
            {
                geos.Add(LegGarment(p, 1, yBot));
                geos.Add(LegGarment(p, -1, yBot));
            }
            return MeshBuilder.Merge(geos.ToArray());
        }

        public static Mesh BuildFull(AvatarParams p)
        {
            if (p.full == FullGarment.None) return null;
            BodyScale(p, out float h, out float g, out float fem);
            const int segs = 32;
            const int n = 18;
            float y0 = 1.36f * h;
            float y1 = p.full == FullGarment.Dress ? 0.4f * h : 0.8f * h;
            float chest = MathUtil.Lerp(0.74f, 1.48f, p.chest);
            float waist = MathUtil.Lerp(0.72f, 1.3f, p.waist);
            float hip = MathUtil.Lerp(0.8f, 1.34f, p.hips);
            var rings = new List<float[]>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float y = MathUtil.Lerp(y0, y1, t);
                float rx = 0.16f * g;
                float rz = 0.13f * g;
                if (t < 0.25f)
                {
                    rx *= MathUtil.Lerp(1.05f, waist, t / 0.25f);
                    rz *= MathUtil.Lerp(0.95f + chest * 0.15f, 0.9f, t / 0.25f);
                }
                else if (t < 0.45f)
                {
                    rx *= MathUtil.Lerp(waist, hip, (t - 0.25f) / 0.2f);
                    rz *= MathUtil.Lerp(0.9f, 1.05f, (t - 0.25f) / 0.2f);
                }
                else
                {
                    float flare = p.full == FullGarment.Dress
                        ? MathUtil.Lerp(1f, 1.55f, (t - 0.45f) / 0.55f)
                        : MathUtil.Lerp(1f, 0.7f, (t - 0.45f) / 0.55f);
                    rx *= hip * flare;
                    rz *= flare;
                }
                rings.Add(MeshBuilder.Ring(y, rx, rz, segs, fem * 0.018f));
            }
            return MeshBuilder.Loft(rings, segs);
        }
    }
}
