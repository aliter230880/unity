using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forma
{
    public class MeshBuilder
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<byte> Parts = new List<byte>();
        public readonly List<int> Indices = new List<int>();

        public int Vert(float x, float y, float z, float u, float v, byte part)
        {
            int i = Positions.Count;
            Positions.Add(new Vector3(x, y, z));
            UVs.Add(new Vector2(u, v));
            Parts.Add(part);
            return i;
        }

        public void Tri(int a, int b, int c)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
        }

        public void Tube(List<Vector3> path, Func<float, float, float> radius, int segs, Func<float, float, byte> partFn, bool caps = true)
        {
            int rings = path.Count;
            int start = Positions.Count;
            var n = Vector3.zero;
            var b = Vector3.zero;
            var tng = Vector3.zero;
            var normal = new Vector3(0, 0, 1);

            for (int i = 0; i < rings; i++)
            {
                var p = path[i];
                if (i == 0) tng = path[1] - p;
                else if (i == rings - 1) tng = p - path[i - 1];
                else tng = path[i + 1] - path[i - 1];
                if (tng.sqrMagnitude < 1e-10f) tng = new Vector3(0, 1, 0);
                tng.Normalize();
                normal += tng * -Vector3.Dot(normal, tng);
                if (normal.sqrMagnitude < 1e-8f)
                {
                    normal = Mathf.Abs(tng.y) < 0.9f ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);
                    normal += tng * -Vector3.Dot(normal, tng);
                }
                normal.Normalize();
                b = Vector3.Cross(tng, normal).normalized;
                n = Vector3.Cross(b, tng).normalized;
                float t = i / (float)(rings - 1);
                for (int j = 0; j < segs; j++)
                {
                    float theta = (j / (float)segs) * Mathf.PI * 2f;
                    float r = radius(t, theta);
                    float ct = Mathf.Cos(theta);
                    float st = Mathf.Sin(theta);
                    Vert(
                        p.x + (n.x * ct + b.x * st) * r,
                        p.y + (n.y * ct + b.y * st) * r,
                        p.z + (n.z * ct + b.z * st) * r,
                        j / (float)segs, t, partFn(t, theta));
                }
            }

            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < segs; j++)
                {
                    int a = start + i * segs + j;
                    int c = start + i * segs + ((j + 1) % segs);
                    int d = start + (i + 1) * segs + j;
                    int e = start + (i + 1) * segs + ((j + 1) % segs);
                    Tri(a, d, c);
                    Tri(c, d, e);
                }
            }

            if (caps)
            {
                var first = path[0];
                var last = path[rings - 1];
                int cap0 = Vert(first.x, first.y, first.z, 0.5f, 0f, partFn(0, 0));
                int cap1 = Vert(last.x, last.y, last.z, 0.5f, 1f, partFn(1, 0));
                for (int j = 0; j < segs; j++)
                {
                    int a = start + j;
                    int c = start + ((j + 1) % segs);
                    Tri(cap0, c, a);
                    int a2 = start + (rings - 1) * segs + j;
                    int c2 = start + (rings - 1) * segs + ((j + 1) % segs);
                    Tri(cap1, a2, c2);
                }
            }
        }

        public void Ellipsoid(float cx, float cy, float cz, float rx, float ry, float rz, int segs, int rings,
            Func<float, float, float, byte> partFn,
            Func<float, float, float, float, float, float, Vector3> sculpt = null)
        {
            int start = Positions.Count;
            for (int i = 0; i <= rings; i++)
            {
                float v = i / (float)rings;
                float phi = v * Mathf.PI;
                float sy = Mathf.Cos(phi);
                float sr = Mathf.Sin(phi);
                for (int j = 0; j <= segs; j++)
                {
                    float u = j / (float)segs;
                    float theta = u * Mathf.PI * 2f;
                    float nx = sr * Mathf.Sin(theta);
                    float ny = sy;
                    float nz = sr * Mathf.Cos(theta);
                    float x = cx + nx * rx;
                    float y = cy + ny * ry;
                    float z = cz + nz * rz;
                    if (sculpt != null)
                    {
                        var s = sculpt(x, y, z, nx, ny, nz);
                        x = s.x; y = s.y; z = s.z;
                    }
                    Vert(x, y, z, u, v, partFn(nx, ny, nz));
                }
            }
            int stride = segs + 1;
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segs; j++)
                {
                    int a = start + i * stride + j;
                    int c = a + 1;
                    int d = a + stride;
                    int e = d + 1;
                    Tri(a, d, c);
                    Tri(c, d, e);
                }
            }
        }

        public Mesh ToUnityMesh(string meshName = "FORMA")
        {
            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            var verts = new Vector3[Positions.Count];
            for (int i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];
                verts[i] = new Vector3(p.x, p.y, -p.z);
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, UVs);
            mesh.SetTriangles(Indices, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh Merge(params Mesh[] meshes)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (var m in meshes)
            {
                if (m == null) continue;
                int baseIndex = verts.Count;
                verts.AddRange(m.vertices);
                norms.AddRange(m.normals);
                var mu = m.uv;
                if (mu != null && mu.Length == m.vertexCount) uvs.AddRange(mu);
                else for (int i = 0; i < m.vertexCount; i++) uvs.Add(Vector2.zero);
                var t = m.triangles;
                for (int i = 0; i < t.Length; i++) tris.Add(t[i] + baseIndex);
            }
            var outMesh = new Mesh { name = "FORMA.Merged", indexFormat = IndexFormat.UInt32 };
            outMesh.SetVertices(verts);
            outMesh.SetNormals(norms);
            outMesh.SetUVs(0, uvs);
            outMesh.SetTriangles(tris, 0, true);
            outMesh.RecalculateBounds();
            return outMesh;
        }

        public static Mesh Loft(List<float[]> rings, int segs, bool flipZ = true)
        {
            var positions = new List<Vector3>();
            var indices = new List<int>();
            foreach (var r in rings)
            {
                for (int i = 0; i < r.Length; i += 3)
                {
                    float x = r[i], y = r[i + 1], z = r[i + 2];
                    positions.Add(flipZ ? new Vector3(x, y, -z) : new Vector3(x, y, z));
                }
            }
            int stride = segs + 1;
            for (int i = 0; i < rings.Count - 1; i++)
            {
                for (int j = 0; j < segs; j++)
                {
                    int a = i * stride + j;
                    int c = a + 1;
                    int d = (i + 1) * stride + j;
                    int e = d + 1;
                    indices.Add(a); indices.Add(d); indices.Add(c);
                    indices.Add(c); indices.Add(d); indices.Add(e);
                }
            }
            var mesh = new Mesh { name = "FORMA.Loft", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static float[] Ring(float y, float rx, float rz, int segs, float cz = 0)
        {
            var pts = new float[(segs + 1) * 3];
            for (int j = 0; j <= segs; j++)
            {
                float t = j / (float)segs;
                float a = t * Mathf.PI * 2f;
                int i = j * 3;
                pts[i] = Mathf.Sin(a) * rx;
                pts[i + 1] = y;
                pts[i + 2] = Mathf.Cos(a) * rz + cz;
            }
            return pts;
        }

        public static Mesh TubeFromPoints(Vector3[] pts, float radius, int tubular = 10, int radial = 8)
        {
            var curve = new Vector3[tubular + 1];
            for (int i = 0; i <= tubular; i++)
                curve[i] = Catmull(pts, i / (float)tubular);
            var b = new MeshBuilder();
            var path = new List<Vector3>(curve);
            b.Tube(path, (t, th) => radius, radial, (t, th) => 0, false);
            return b.ToUnityMesh("FORMA.Tube");
        }

        static Vector3 Catmull(Vector3[] pts, float t)
        {
            if (pts.Length == 1) return pts[0];
            float x = t * (pts.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(x), pts.Length - 2);
            float u = x - i;
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u +
                (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
        }
    }
}
