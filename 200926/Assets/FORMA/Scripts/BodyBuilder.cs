using System.Collections.Generic;
using UnityEngine;

namespace Forma
{
    public class BuiltBody
    {
        public Mesh Mesh;
        public Vector3[] Base;
        public byte[] Parts;
        public Color[] Colors;
        public Sex Sex;
    }

    public static class BodyBuilder
    {
        static readonly float[,] FemaleRxK =
        {
            {0.88f,0.072f},{0.96f,0.162f},{1.04f,0.118f},{1.12f,0.108f},
            {1.22f,0.132f},{1.30f,0.148f},{1.38f,0.052f},{1.50f,0.040f}
        };
        static readonly float[,] FemaleRzK =
        {
            {0.88f,0.078f},{0.96f,0.138f},{1.04f,0.112f},{1.12f,0.100f},
            {1.22f,0.168f},{1.30f,0.110f},{1.38f,0.050f},{1.50f,0.042f}
        };
        static readonly float[,] MaleRxK =
        {
            {0.90f,0.080f},{0.98f,0.148f},{1.08f,0.132f},{1.16f,0.142f},
            {1.28f,0.205f},{1.38f,0.220f},{1.46f,0.070f},{1.58f,0.052f}
        };
        static readonly float[,] MaleRzK =
        {
            {0.90f,0.085f},{0.98f,0.140f},{1.08f,0.138f},{1.18f,0.155f},
            {1.28f,0.168f},{1.38f,0.130f},{1.46f,0.068f},{1.58f,0.050f}
        };

        static float FemaleRx(float y) => MathUtil.Kf(FemaleRxK, y);
        static float FemaleRz(float y) => MathUtil.Kf(FemaleRzK, y);
        static float MaleRx(float y) => MathUtil.Kf(MaleRxK, y);
        static float MaleRz(float y) => MathUtil.Kf(MaleRzK, y);

        static byte TorsoPart(float y, float theta, Sex sex)
        {
            bool front = Mathf.Cos(theta) > 0.12f;
            if (sex == Sex.Female)
            {
                if (y > 1.4f) return BodyPart.Neck;
                if (y > 1.12f && front) return BodyPart.Chest;
                if (y > 1.06f && y < 1.2f) return BodyPart.Waist;
                if (y > 0.96f && y < 1.12f && front) return BodyPart.Belly;
                if (y < 0.97f && Mathf.Cos(theta) < -0.2f) return BodyPart.Butt;
                if (y < 1.02f) return BodyPart.Hip;
                return BodyPart.Waist;
            }
            if (y > 1.46f) return BodyPart.Neck;
            if (y > 1.2f && front) return BodyPart.Chest;
            if (y > 1.02f && y < 1.2f && front) return BodyPart.Belly;
            if (y < 1.0f && Mathf.Cos(theta) < -0.15f) return BodyPart.Butt;
            if (y < 1.04f) return BodyPart.Hip;
            return BodyPart.Waist;
        }

        static void AddTorso(MeshBuilder b, Sex sex)
        {
            float y0 = sex == Sex.Female ? 0.88f : 0.9f;
            float y1 = sex == Sex.Female ? 1.52f : 1.6f;
            int rings = 88;
            var path = new List<Vector3>(rings);
            for (int i = 0; i < rings; i++)
                path.Add(new Vector3(0, MathUtil.Lerp(y0, y1, i / (float)(rings - 1)), 0));
            float fem = sex == Sex.Female ? 1f : 0f;
            b.Tube(path, (t, theta) =>
            {
                float y = MathUtil.Lerp(y0, y1, t);
                float rx = sex == Sex.Female ? FemaleRx(y) : MaleRx(y);
                float rz = sex == Sex.Female ? FemaleRz(y) : MaleRz(y);
                float ct = Mathf.Cos(theta);
                float st = Mathf.Sin(theta);
                float r = Mathf.Sqrt((st * rx) * (st * rx) + (ct * rz) * (ct * rz));
                if (fem > 0)
                {
                    r += MathUtil.Gauss(Mathf.Abs(st) - 0.5f, 0.22f) * MathUtil.Gauss(y - 1.22f, 0.055f) * Mathf.Max(0, ct) * 0.07f;
                    r += MathUtil.Gauss(Mathf.Abs(st) - 0.42f, 0.2f) * MathUtil.Gauss(y - 0.94f, 0.05f) * Mathf.Max(0, -ct) * 0.055f;
                    r += MathUtil.Gauss(y - 1.34f, 0.03f) * Mathf.Abs(st) * 0.01f;
                }
                else
                {
                    r += MathUtil.Gauss(Mathf.Abs(st) - 0.38f, 0.2f) * MathUtil.Gauss(y - 1.3f, 0.055f) * Mathf.Max(0, ct) * 0.055f;
                    float absY = (y - 1.05f) / 0.2f;
                    if (absY > 0 && absY < 1 && ct > 0.25f)
                        r += Mathf.Abs(Mathf.Sin(absY * Mathf.PI * 4.2f)) * MathUtil.Gauss(st, 0.45f) * 0.014f * ct;
                    r += MathUtil.Gauss(Mathf.Abs(st) - 0.85f, 0.16f) * MathUtil.Gauss(y - 1.18f, 0.08f) * 0.02f;
                    r += MathUtil.Gauss(Mathf.Abs(st) - 0.92f, 0.12f) * MathUtil.Gauss(y - 1.4f, 0.05f) * 0.028f;
                    r += MathUtil.Gauss(Mathf.Abs(st) - 0.4f, 0.22f) * MathUtil.Gauss(y - 0.96f, 0.05f) * Mathf.Max(0, -ct) * 0.04f;
                    r += MathUtil.Gauss(y - 1.42f, 0.025f) * (1f - Mathf.Abs(st)) * 0.012f;
                }
                return r;
            }, 64, (t, theta) => TorsoPart(MathUtil.Lerp(y0, y1, t), theta, sex), false);
        }

        static void AddLeg(MeshBuilder b, float side, Sex sex)
        {
            float hipY = sex == Sex.Female ? 0.9f : 0.94f;
            float hipX = sex == Sex.Female ? 0.108f : 0.1f;
            int rings = 64;
            var path = new List<Vector3>(rings);
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                float y = MathUtil.Lerp(hipY, 0.045f, t);
                float x = side * MathUtil.Lerp(hipX, 0.05f, t * t * 0.85f + t * 0.15f);
                float z = sex == Sex.Female ? 0.012f : 0.018f;
                path.Add(new Vector3(x, y, z));
            }
            float jacked = sex == Sex.Male ? 1.18f : 1f;
            b.Tube(path, (t, theta) =>
            {
                float ct = Mathf.Cos(theta);
                float st = Mathf.Sin(theta);
                float inner = Mathf.Max(0, -side * st);
                float thigh = MathUtil.Lerp((sex == Sex.Female ? 0.06f : 0.072f) * jacked, 0.05f, MathUtil.Smoothstep(0, 0.42f, t)) * (1f - inner * 0.16f);
                float quad = MathUtil.Gauss(ct - 0.4f, 0.35f) * MathUtil.Gauss(t - 0.22f, 0.16f) * 0.016f * jacked;
                float ham = MathUtil.Gauss(-ct - 0.3f, 0.35f) * MathUtil.Gauss(t - 0.22f, 0.16f) * 0.012f * jacked;
                float knee = 0.038f + 0.01f * MathUtil.Gauss(t - 0.5f, 0.05f);
                float calf = (0.036f + 0.026f * Mathf.Sin(Mathf.Min(1, Mathf.Max(0, (t - 0.52f) / 0.28f)) * Mathf.PI) * (0.45f + 0.55f * Mathf.Max(0, -ct))) * jacked;
                float ankle = MathUtil.Lerp(0.034f, 0.024f, MathUtil.Smoothstep(0.84f, 1f, t));
                float r = t < 0.48f ? MathUtil.Lerp(thigh, knee, t / 0.48f)
                    : t < 0.84f ? MathUtil.Lerp(knee, calf, (t - 0.48f) / 0.36f)
                    : MathUtil.Lerp(calf, ankle, (t - 0.84f) / 0.16f);
                r += quad + ham;
                r *= 0.88f + 0.12f * Mathf.Abs(st);
                return r;
            }, 36, (t, th) => t < 0.5f ? BodyPart.Thigh : BodyPart.Calf, true);
        }

        static void AddArm(MeshBuilder b, float side, Sex sex)
        {
            float shY = sex == Sex.Female ? 1.34f : 1.42f;
            float shX = sex == Sex.Female ? 0.168f : 0.215f;
            int rings = 56;
            var path = new List<Vector3>(rings);
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                float elbow = MathUtil.Smoothstep(0.35f, 0.62f, t);
                path.Add(new Vector3(
                    side * (shX + MathUtil.Lerp(0.01f, 0.045f, t)),
                    MathUtil.Lerp(shY, sex == Sex.Female ? 0.96f : 0.98f, t) - elbow * 0.012f,
                    0.03f + Mathf.Sin(t * Mathf.PI) * 0.025f));
            }
            float jacked = sex == Sex.Male ? 1.22f : 1f;
            b.Tube(path, (t, theta) =>
            {
                float ct = Mathf.Cos(theta);
                float deltoid = MathUtil.Gauss(t - 0.06f, 0.08f) * 0.018f * jacked;
                float bicep = MathUtil.Gauss(t - 0.28f, 0.12f) * MathUtil.Gauss(ct - 0.35f, 0.5f) * 0.016f * jacked;
                float tri = MathUtil.Gauss(t - 0.3f, 0.12f) * MathUtil.Gauss(-ct - 0.2f, 0.5f) * 0.014f * jacked;
                float upper = MathUtil.Lerp((sex == Sex.Female ? 0.036f : 0.05f) * jacked, 0.034f, t);
                float elbow = 0.028f;
                float fore = MathUtil.Lerp(0.032f * jacked, 0.02f, t);
                float r = t < 0.48f ? MathUtil.Lerp(upper, elbow, t / 0.48f) : MathUtil.Lerp(elbow, fore, (t - 0.48f) / 0.52f);
                r += deltoid + bicep + tri;
                return r;
            }, 30, (t, th) => t < 0.5f ? BodyPart.UpperArm : BodyPart.Forearm, false);
        }

        static void AddHand(MeshBuilder b, float side, Sex sex)
        {
            float shY = sex == Sex.Female ? 1.34f : 1.42f;
            float shX = sex == Sex.Female ? 0.168f : 0.215f;
            float wx = side * (shX + 0.045f);
            float wy = sex == Sex.Female ? 0.96f : 0.98f;
            float wz = 0.03f;
            float s = sex == Sex.Female ? 0.9f : 1f;
            b.Ellipsoid(wx, wy - 0.02f, wz, 0.028f * s, 0.05f * s, 0.016f * s, 20, 16, (nx, ny, nz) => BodyPart.Hand);
            float[] lens = { 0.055f, 0.062f, 0.058f, 0.05f, 0.042f };
            float[] spreads = { -0.028f, -0.01f, 0.008f, 0.024f, 0.038f };
            for (int f = 0; f < 5; f++)
            {
                var path = new List<Vector3>(10);
                for (int i = 0; i < 10; i++)
                {
                    float t = i / 9f;
                    path.Add(new Vector3(
                        wx + side * 0.01f + spreads[f] * s * (0.3f + t * 0.7f),
                        wy - 0.05f - t * lens[f] * s,
                        wz + 0.004f * Mathf.Sin(t * Mathf.PI)));
                }
                int fi = f;
                b.Tube(path, (t, th) => MathUtil.Lerp(0.0075f, 0.0042f, t) * s, 10, (t, th) => BodyPart.Hand, true);
            }
        }

        static void AddFoot(MeshBuilder b, float side, Sex sex)
        {
            float x = side * 0.05f;
            float s = sex == Sex.Female ? 0.88f : 1f;
            b.Ellipsoid(x, 0.03f, 0.055f, 0.032f * s, 0.024f, 0.095f * s, 24, 16, (nx, ny, nz) => BodyPart.Foot);
            for (int t = 0; t < 5; t++)
            {
                float ox = x + (t - 2) * 0.011f * s;
                b.Ellipsoid(ox, 0.018f, 0.132f, 0.006f * s, 0.006f, 0.018f * s, 10, 8, (nx, ny, nz) => BodyPart.Foot);
            }
        }

        static void AddHead(MeshBuilder b, Sex sex)
        {
            float cy = sex == Sex.Female ? 1.62f : 1.7f;
            float rx = sex == Sex.Female ? 0.086f : 0.092f;
            float ry = sex == Sex.Female ? 0.112f : 0.118f;
            float rz = sex == Sex.Female ? 0.094f : 0.1f;
            float fem = sex == Sex.Female ? 1f : 0f;
            b.Ellipsoid(0, cy, 0.012f, rx, ry, rz, 96, 72, (nx, ny, nz) =>
            {
                if (ny > 0.38f) return BodyPart.Scalp;
                float dNose = Mathf.Sqrt(nx * nx + (ny + 0.12f) * (ny + 0.12f) + (nz - 0.85f) * (nz - 0.85f));
                if (dNose < 0.35f && nz > 0.4f) return BodyPart.Nose;
                float dLip = Mathf.Sqrt((nx * 1.4f) * (nx * 1.4f) + (ny + 0.38f) * (ny + 0.38f) + (nz - 0.8f) * (nz - 0.8f));
                if (dLip < 0.28f && nz > 0.35f) return BodyPart.Lip;
                float dLid = Mathf.Sqrt((Mathf.Abs(nx) - 0.32f) * (Mathf.Abs(nx) - 0.32f) + (ny - 0.08f) * (ny - 0.08f) + (nz - 0.85f) * (nz - 0.85f));
                if (dLid < 0.22f && nz > 0.45f) return BodyPart.Lid;
                return BodyPart.Face;
            }, (x, y, z, nx, ny, nz) =>
            {
                float px = x, py = y, pz = z;
                pz -= (1f - fem) * 0.004f;
                px *= 1f - 0.05f * Mathf.Max(0, nz);
                float jaw = MathUtil.Rbf(px, py - (cy - 0.072f), pz - 0.02f, 0.09f);
                px *= 1f + (0.055f + (1f - fem) * 0.12f) * jaw;
                py -= (0.016f + (1f - fem) * 0.014f) * MathUtil.Rbf(px, py - (cy - 0.1f), pz, 0.08f);
                float chin = MathUtil.Rbf(px, py - (cy - 0.108f), pz - 0.05f, 0.052f);
                pz += chin * (0.03f + (1f - fem) * 0.01f);
                py -= chin * 0.016f;
                float brow = MathUtil.Rbf(Mathf.Abs(px) - 0.03f, py - (cy + 0.032f), pz - 0.07f, 0.05f);
                pz += brow * (0.014f + (1f - fem) * 0.022f);
                py += brow * 0.007f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float sock = MathUtil.Rbf(px - s * 0.033f, py - (cy + 0.012f), pz - 0.078f, 0.032f);
                    pz -= sock * 0.026f;
                    py -= sock * 0.005f;
                }
                float nose = MathUtil.Rbf(px, py - (cy - 0.008f), Mathf.Max(0, pz - 0.04f), 0.058f);
                pz += nose * (0.046f + fem * 0.004f);
                py -= nose * 0.015f;
                px *= 1f - nose * 0.42f;
                float nostril = MathUtil.Rbf(Mathf.Abs(px) - 0.013f, py - (cy - 0.028f), pz - 0.088f, 0.02f);
                px += Mathf.Sign(px == 0 ? 1 : px) * nostril * 0.012f;
                pz += nostril * 0.01f;
                float cheek = MathUtil.Rbf(Mathf.Abs(px) - 0.05f, py - (cy - 0.006f), pz - 0.038f, 0.055f);
                px += Mathf.Sign(px == 0 ? 1 : px) * cheek * (0.012f + fem * 0.012f);
                pz += cheek * fem * 0.01f;
                float mouth = MathUtil.Rbf(px * 1.6f, py - (cy - 0.048f), pz - 0.072f, 0.04f);
                pz += mouth * 0.012f;
                float upperLip = MathUtil.Rbf(px * 1.8f, py - (cy - 0.04f), pz - 0.084f, 0.028f);
                pz += upperLip * (0.014f + fem * 0.012f);
                float lowerLip = MathUtil.Rbf(px * 1.7f, py - (cy - 0.056f), pz - 0.082f, 0.03f);
                pz += lowerLip * (0.018f + fem * 0.012f);
                py -= lowerLip * 0.005f;
                if (ny > 0.12f && nz > 0.15f)
                    py += MathUtil.Rbf(Mathf.Abs(px) - 0.03f, py - (cy + 0.038f), pz - 0.07f, 0.04f) * fem * 0.006f;
                return new Vector3(px, py, pz);
            });

            for (int s = -1; s <= 1; s += 2)
            {
                var earPath = new List<Vector3>(12);
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11f;
                    float a = MathUtil.Lerp(-0.6f, 0.72f, t);
                    earPath.Add(new Vector3(s * (rx + 0.005f), cy + Mathf.Sin(a) * 0.04f, -0.012f + (1f - Mathf.Cos(a)) * 0.014f));
                }
                b.Tube(earPath, (t, theta) => 0.013f * (0.55f + 0.45f * Mathf.Sin(t * Mathf.PI)) * (0.65f + 0.35f * Mathf.Abs(Mathf.Cos(theta))), 18, (t, th) => BodyPart.Ear, true);
            }
        }

        public static BuiltBody Build(Sex sex)
        {
            var b = new MeshBuilder();
            AddTorso(b, sex);
            AddLeg(b, -1, sex);
            AddLeg(b, 1, sex);
            AddArm(b, -1, sex);
            AddArm(b, 1, sex);
            AddHand(b, -1, sex);
            AddHand(b, 1, sex);
            AddFoot(b, -1, sex);
            AddFoot(b, 1, sex);
            AddHead(b, sex);

            var mesh = b.ToUnityMesh("FORMA.Body");
            var built = new BuiltBody
            {
                Mesh = mesh,
                Base = b.Positions.ToArray(),
                Parts = b.Parts.ToArray(),
                Colors = new Color[b.Positions.Count],
                Sex = sex
            };
            return built;
        }

        public static void ApplyMorphs(BuiltBody built, AvatarParams p, AvatarMaps maps)
        {
            var pos = new Vector3[built.Base.Length];
            var parts = built.Parts;
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            float g = MathUtil.Lerp(0.86f, 1.18f, p.build);
            float mus = p.muscular;
            float fat = p.fat;
            float fem = p.sex == Sex.Female ? 1f : 0f;
            float headY = p.sex == Sex.Female ? 1.62f : 1.7f;
            int count = parts.Length;

            for (int i = 0; i < count; i++)
            {
                float x = built.Base[i].x;
                float y = built.Base[i].y;
                float z = built.Base[i].z;
                byte part = parts[i];
                float sx = x == 0 ? 0 : Mathf.Sign(x);

                y *= h;
                float legLen = MathUtil.Lerp(0.92f, 1.14f, p.legs);
                if (part == BodyPart.Thigh || part == BodyPart.Calf || part == BodyPart.Foot)
                    y = MathUtil.Lerp(0.88f * h, y, legLen);

                float girth = g * (1f + fat * 0.14f);
                if (part != BodyPart.Scalp && part != BodyPart.Face && part != BodyPart.Ear && part != BodyPart.Lip && part != BodyPart.Nose && part != BodyPart.Lid)
                {
                    x *= girth;
                    z *= 0.96f + girth * 0.04f;
                }

                if (part == BodyPart.Neck)
                {
                    float n = MathUtil.Lerp(0.78f, 1.32f, p.neck);
                    x *= n; z *= n;
                    y += MathUtil.Off(p.neckLength) * 0.032f;
                }
                if (part == BodyPart.Chest)
                {
                    float c = MathUtil.Lerp(0.74f, 1.48f, p.chest);
                    z += (c - 1f) * (0.045f + fem * 0.07f) * (0.35f + 0.65f * Mathf.Max(0, z));
                    x += sx * (c - 1f) * 0.022f * fem;
                    x += sx * MathUtil.Off(p.chestSep) * 0.02f * fem;
                    z += mus * 0.038f * (1f - fem) * Mathf.Max(0, z);
                }
                if (part == BodyPart.Belly)
                {
                    z += p.belly * 0.075f * (0.5f + 0.5f * Mathf.Max(0, z));
                    x *= 1f + p.belly * 0.08f;
                    if (fem < 0.5f)
                    {
                        float packs = Mathf.Abs(Mathf.Sin((y - 1.02f * h) * 42f)) * MathUtil.Gauss(x, 0.06f);
                        z += packs * mus * 0.012f * Mathf.Max(0, z);
                    }
                }
                if (part == BodyPart.Waist)
                {
                    float w = MathUtil.Lerp(0.72f, 1.3f, p.waist);
                    x *= w;
                    z *= 0.88f + w * 0.12f;
                    if (fem < 0.5f)
                    {
                        x *= 1f - mus * 0.04f;
                        z += mus * 0.01f * Mathf.Abs(x);
                    }
                }
                if (part == BodyPart.Hip) x *= MathUtil.Lerp(0.8f, 1.34f, p.hips);
                if (part == BodyPart.Butt)
                {
                    z -= MathUtil.Lerp(0.68f, 1.55f, p.buttocks) * 0.038f * Mathf.Max(0, -z + 0.04f);
                    x *= 1f + MathUtil.Off(p.buttocks) * 0.09f;
                }
                if (part == BodyPart.Thigh)
                {
                    float th = MathUtil.Lerp(0.8f, 1.32f, p.thighs) * (1f + mus * 0.12f);
                    float cx = sx * 0.09f * h;
                    x = cx + (x - cx) * th;
                    z *= th;
                }
                if (part == BodyPart.Calf)
                {
                    float cv = MathUtil.Lerp(0.8f, 1.36f, p.calves) * (1f + mus * 0.14f);
                    z *= cv;
                    x = sx * 0.04f * h + (x - sx * 0.04f * h) * cv;
                }
                if (part == BodyPart.UpperArm)
                {
                    float a = MathUtil.Lerp(0.78f, 1.42f, p.arms) * (1f + mus * 0.22f);
                    float sh = MathUtil.Lerp(0.88f, 1.28f, p.shoulders);
                    x *= sh; z *= a;
                    x += sx * (a - 1f) * 0.02f;
                }
                if (part == BodyPart.Forearm)
                {
                    float a = MathUtil.Lerp(0.78f, 1.34f, p.forearms) * (1f + mus * 0.14f);
                    z *= a;
                    x *= 1f + (a - 1f) * 0.4f;
                }
                if (part == BodyPart.Hand)
                {
                    float s = MathUtil.Lerp(0.84f, 1.22f, p.hands);
                    float sh = MathUtil.Lerp(0.88f, 1.28f, p.shoulders);
                    float hx = sx * ((p.sex == Sex.Female ? 0.213f : 0.26f) * sh);
                    float hy = (p.sex == Sex.Female ? 0.96f : 0.98f) * h;
                    x = hx + (x - sx * (p.sex == Sex.Female ? 0.213f : 0.26f)) * s;
                    y = hy + (y - (p.sex == Sex.Female ? 0.96f : 0.98f)) * s;
                    z = 0.03f + (z - 0.03f) * s;
                }
                if (part == BodyPart.Foot)
                {
                    float s = MathUtil.Lerp(0.84f, 1.24f, p.feet);
                    x *= s;
                    z = 0.055f + (z - 0.055f) * s;
                }

                if (part == BodyPart.Scalp || part == BodyPart.Face || part == BodyPart.Lip || part == BodyPart.Nose || part == BodyPart.Ear || part == BodyPart.Lid)
                {
                    float hc = headY * h;
                    float lx = x, ly = y - hc, lz = z - 0.012f;
                    lx *= MathUtil.Lerp(0.88f, 1.14f, p.headWidth);
                    ly *= MathUtil.Lerp(0.88f, 1.14f, p.headHeight);
                    lz *= MathUtil.Lerp(0.88f, 1.14f, p.headDepth);
                    if (ly > 0.02f) ly *= MathUtil.Lerp(0.9f, 1.16f, p.forehead);
                    float brow = MathUtil.Rbf(Mathf.Abs(lx) - 0.03f, ly - 0.03f, lz - 0.07f, 0.05f);
                    lz += brow * MathUtil.Off(p.browRidge) * 0.02f;
                    ly += brow * MathUtil.Off(p.browArch) * 0.01f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float sock = MathUtil.Rbf(lx - s * MathUtil.Lerp(0.026f, 0.04f, p.eyeSpacing), ly - MathUtil.Lerp(-0.01f, 0.03f, p.eyeHeight), lz - 0.07f, MathUtil.Lerp(0.024f, 0.04f, p.eyeSize));
                        lz -= sock * MathUtil.Lerp(0.006f, 0.022f, p.eyeDepth);
                        ly += sock * MathUtil.Off(p.eyeShape) * 0.008f;
                    }
                    float nose = MathUtil.Rbf(lx, ly + 0.012f, Mathf.Max(0, lz - 0.04f), 0.06f);
                    lz += nose * MathUtil.Off(p.noseLength) * 0.03f;
                    lx *= 1f + nose * MathUtil.Off(p.noseWidth) * 0.5f;
                    ly += nose * MathUtil.Off(p.noseBridge) * 0.012f;
                    float tip = MathUtil.Rbf(lx, ly + 0.028f, lz - 0.08f, 0.03f);
                    lz += tip * MathUtil.Off(p.noseTip) * 0.016f;
                    float nos = MathUtil.Rbf(Mathf.Abs(lx) - 0.012f, ly + 0.03f, lz - 0.08f, 0.022f);
                    lx += sx * nos * MathUtil.Off(p.nostrils) * 0.012f;
                    float cheekB = MathUtil.Rbf(Mathf.Abs(lx) - 0.05f, ly + 0.005f, lz - 0.03f, 0.055f);
                    lx += sx * cheekB * MathUtil.Off(p.cheekbones) * 0.016f;
                    lz += cheekB * MathUtil.Off(p.cheeks) * 0.012f;
                    float jowl = MathUtil.Rbf(Mathf.Abs(lx) - 0.045f, ly + 0.055f, lz - 0.02f, 0.05f);
                    lx += sx * jowl * p.jowls * 0.02f;
                    ly -= jowl * p.jowls * 0.01f;
                    float lips = MathUtil.Rbf(lx * 1.7f, ly + 0.042f, lz - 0.07f, 0.045f);
                    lx *= 1f + lips * MathUtil.Off(p.lipWidth) * 0.35f;
                    float up = MathUtil.Rbf(lx * 1.8f, ly + 0.036f, lz - 0.078f, 0.03f);
                    lz += up * MathUtil.Off(p.lipUpper) * 0.014f;
                    ly += up * MathUtil.Off(p.lipUpper) * 0.006f;
                    float lo = MathUtil.Rbf(lx * 1.7f, ly + 0.05f, lz - 0.076f, 0.032f);
                    lz += lo * MathUtil.Off(p.lipLower) * 0.016f;
                    ly -= lo * MathUtil.Off(p.lipLower) * 0.005f;
                    float jaw = MathUtil.Rbf(lx, ly + 0.07f, lz, 0.09f);
                    lx *= 1f + jaw * MathUtil.Off(p.jawWidth) * 0.16f;
                    ly -= jaw * MathUtil.Off(p.jawLength) * 0.016f;
                    float chin = MathUtil.Rbf(lx, ly + 0.1f, lz - 0.04f, 0.05f);
                    lx *= 1f + chin * MathUtil.Off(p.chinWidth) * 0.2f;
                    ly -= chin * MathUtil.Off(p.chinHeight) * 0.016f;
                    lz += chin * MathUtil.Off(p.chinProjection) * 0.018f;
                    if (part == BodyPart.Ear)
                    {
                        float es = MathUtil.Lerp(0.82f, 1.25f, p.earSize);
                        lx = sx * 0.09f * MathUtil.Lerp(0.88f, 1.14f, p.headWidth) + (lx - sx * 0.09f) * es;
                        ly *= es;
                        lz += MathUtil.Off(p.earStick) * 0.012f;
                    }
                    x = lx; y = ly + hc; z = lz + 0.012f;
                }

                if (part == BodyPart.UpperArm || part == BodyPart.Chest)
                    x *= MathUtil.Lerp(0.9f, 1.22f, p.shoulders);

                pos[i] = new Vector3(x, y, -z);
            }

            built.Mesh.SetVertices(pos);
            built.Mesh.RecalculateNormals();
            built.Mesh.RecalculateBounds();
            PaintSkin(built, p, maps, pos);
        }

        public static void PaintSkin(BuiltBody built, AvatarParams p, AvatarMaps maps, Vector3[] unityPos)
        {
            var parts = built.Parts;
            var colors = built.Colors;
            var nrm = built.Mesh.normals;
            Color skin = MathUtil.Hex(p.skin);
            Color lip = MathUtil.Hex(p.lipTint);
            float warm = p.undertone;
            float blush = p.blush;
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            float headY = (p.sex == Sex.Female ? 1.62f : 1.7f) * h;
            var front = p.sex == Sex.Female ? maps?.FemaleFront : maps?.MaleFront;
            var back = p.sex == Sex.Female ? maps?.FemaleBack : maps?.MaleBack;
            var face = p.sex == Sex.Female ? maps?.FemaleFace : maps?.MaleFace;
            float bodyH = 1.82f * h;

            for (int i = 0; i < parts.Length; i++)
            {
                byte part = parts[i];
                // unityPos already has Z flipped; convert back to three.js space for sampling
                float x = unityPos[i].x;
                float y = unityPos[i].y;
                float z = -unityPos[i].z;
                float nz = nrm != null && nrm.Length == parts.Length ? -nrm[i].z : 0.5f;
                float r = skin.r, g = skin.g, b = skin.b;
                r += (warm - 0.5f) * 0.06f;
                b -= (warm - 0.5f) * 0.05f;

                if (front != null && back != null)
                {
                    float u = Mathf.Clamp01(x / ((p.sex == Sex.Female ? 0.26f : 0.32f) * h) + 0.5f);
                    float v = Mathf.Clamp01(y / bodyH);
                    var pf = front.Sample(u, v);
                    var pb = back.Sample(u, v);
                    float fz = Mathf.Clamp(nz * 1.4f, -1f, 1f);
                    float wFront = MathUtil.Smoothstep(-0.05f, 0.45f, fz) * pf.a;
                    float wBack = MathUtil.Smoothstep(-0.45f, 0.05f, -fz) * pb.a;
                    float w = Mathf.Max(wFront, wBack);
                    if (w > 0.04f)
                    {
                        var src = wFront >= wBack ? pf : pb;
                        float mix = Mathf.Min(0.92f, w * 0.95f);
                        r = r * (1f - mix) + src.r * mix;
                        g = g * (1f - mix) + src.g * mix;
                        b = b * (1f - mix) + src.b * mix;
                    }
                }

                bool isFace = part == BodyPart.Face || part == BodyPart.Lip || part == BodyPart.Nose || part == BodyPart.Lid || part == BodyPart.Scalp;
                if (face != null && isFace && nz > 0.12f)
                {
                    float fu = Mathf.Clamp01(x / 0.13f + 0.5f);
                    float fv = Mathf.Clamp01((y - (headY - 0.13f)) / 0.26f);
                    var s = face.Sample(fu, fv);
                    if (s.a > 0.15f)
                    {
                        float mix = Mathf.Min(0.88f, s.a * MathUtil.Smoothstep(0.12f, 0.55f, nz));
                        r = r * (1f - mix) + s.r * mix;
                        g = g * (1f - mix) + s.g * mix;
                        b = b * (1f - mix) + s.b * mix;
                    }
                }

                if (part == BodyPart.Lip)
                {
                    const float w = 0.4f;
                    r = r * (1f - w) + lip.r * w;
                    g = g * (1f - w) + lip.g * w;
                    b = b * (1f - w) + lip.b * w;
                }
                if (part == BodyPart.Ear || part == BodyPart.Nose)
                {
                    r += 0.035f; g -= 0.008f;
                }
                float cheek = MathUtil.Rbf(Mathf.Abs(x) - 0.05f, y - (headY - 0.02f), z - 0.05f, 0.06f);
                r += cheek * blush * 0.12f;
                g -= cheek * blush * 0.04f;
                if (part == BodyPart.Hand || part == BodyPart.Foot)
                {
                    r += 0.02f; g -= 0.01f;
                }
                colors[i] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
            }
            built.Mesh.SetColors(colors);
        }

        public static void EyeAnchors(AvatarParams p, out Vector3 left, out Vector3 right, out float scale)
        {
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            float headY = (p.sex == Sex.Female ? 1.632f : 1.712f) * h + MathUtil.Off(p.eyeHeight) * 0.02f;
            float space = MathUtil.Lerp(0.026f, 0.04f, p.eyeSpacing);
            float z = 0.082f * MathUtil.Lerp(0.9f, 1.12f, p.headDepth) - MathUtil.Off(p.eyeDepth) * 0.01f;
            scale = MathUtil.Lerp(0.011f, 0.0155f, p.eyeSize) * (p.sex == Sex.Female ? 1.02f : 0.96f);
            left = new Vector3(-space, headY, -(z - 0.006f));
            right = new Vector3(space, headY, -(z - 0.006f));
        }

        public static Vector3 ScalpCenter(AvatarParams p)
        {
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            return new Vector3(0, (p.sex == Sex.Female ? 1.64f : 1.72f) * h, -0.012f);
        }

        public static Vector3 JawCenter(AvatarParams p)
        {
            float h = MathUtil.Lerp(0.9f, 1.16f, p.height);
            return new Vector3(0, (p.sex == Sex.Female ? 1.52f : 1.58f) * h, -0.04f);
        }
    }
}
