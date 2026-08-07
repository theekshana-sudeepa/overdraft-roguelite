using UnityEngine;

namespace Overdraft.EditorTools
{
    /// <summary>
    /// Builds every mesh in the game from code (docs/04 §2 — all primitives, no textures,
    /// silhouette does 100% of the identification work).
    /// <para>
    /// Generating them rather than authoring them means there are no binary mesh assets to
    /// merge, no ProBuilder dependency, and a shape change is a one-line diff.
    /// </para>
    /// </summary>
    public static class PrimitiveMeshFactory
    {
        /// <summary>Player and Drifter. Points along +Z, roughly unit radius.</summary>
        public static Mesh Triangle()
        {
            Mesh mesh = new Mesh { name = "M_Triangle" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(-0.80f, 0f, -0.70f),
                new Vector3(0.80f, 0f, -0.70f)
            };
            mesh.triangles = new[] { 0, 2, 1 };
            Finish(mesh);
            return mesh;
        }

        /// <summary>Splitter (M2). Four verts, two tris.</summary>
        public static Mesh Diamond()
        {
            Mesh mesh = new Mesh { name = "M_Diamond" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0.7f, 0f, 0f),
                new Vector3(0f, 0f, -1f),
                new Vector3(-0.7f, 0f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            Finish(mesh);
            return mesh;
        }

        /// <summary>Spitter (M2). A hollow square, built as four thin quads.</summary>
        public static Mesh HollowSquare(float thickness = 0.25f)
        {
            Mesh mesh = new Mesh { name = "M_HollowSquare" };

            float o = 1f;
            float i = 1f - thickness;

            Vector3[] verts = new Vector3[8];
            verts[0] = new Vector3(-o, 0f, -o);
            verts[1] = new Vector3(o, 0f, -o);
            verts[2] = new Vector3(o, 0f, o);
            verts[3] = new Vector3(-o, 0f, o);
            verts[4] = new Vector3(-i, 0f, -i);
            verts[5] = new Vector3(i, 0f, -i);
            verts[6] = new Vector3(i, 0f, i);
            verts[7] = new Vector3(-i, 0f, i);

            int[] tris = new int[24];
            int t = 0;
            for (int e = 0; e < 4; e++)
            {
                int a = e;
                int b = (e + 1) % 4;
                int c = 4 + b;
                int d = 4 + a;

                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = a; tris[t++] = c; tris[t++] = d;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            Finish(mesh);
            return mesh;
        }

        /// <summary>Projectile. A unit quad lying in the XZ plane.</summary>
        public static Mesh Quad()
        {
            Mesh mesh = new Mesh { name = "M_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            Finish(mesh);
            return mesh;
        }

        /// <summary>
        /// A bar extending from the LOCAL ORIGIN out along +Z to length 1, width 1 (centred on
        /// X). Unlike Quad (centred at origin), this lets a hazard bar's transform sit at the
        /// arena centre and simply rotate to sweep, with scale controlling width and reach.
        /// </summary>
        public static Mesh Bar()
        {
            Mesh mesh = new Mesh { name = "M_Bar" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 1f),
                new Vector3(-0.5f, 0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            Finish(mesh);
            return mesh;
        }

        /// <summary>Arena floor. A unit-radius disc in the XZ plane.</summary>
        public static Mesh Disc(int segments = 96)
        {
            Mesh mesh = new Mesh { name = "M_Disc" };

            Vector3[] verts = new Vector3[segments + 1];
            verts[0] = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            }

            int[] tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + i;
                tris[i * 3 + 2] = 1 + (i + 1) % segments;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            Finish(mesh);
            return mesh;
        }

        /// <summary>
        /// Bank ring and arena boundary. Outer radius is 1; innerRatio sets the hole.
        /// Vertices start at +Z and wind clockwise so the radial-fill shader's angle maps
        /// straight onto fill progress.
        /// </summary>
        public static Mesh Annulus(float innerRatio, int segments = 128)
        {
            Mesh mesh = new Mesh { name = "M_Annulus" };

            int vertCount = segments * 2;
            Vector3[] verts = new Vector3[vertCount];

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float s = Mathf.Sin(a);
                float c = Mathf.Cos(a);

                verts[i * 2] = new Vector3(s * innerRatio, 0f, c * innerRatio);
                verts[i * 2 + 1] = new Vector3(s, 0f, c);
            }

            int[] tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = ((i + 1) % segments) * 2;
                int i3 = ((i + 1) % segments) * 2 + 1;

                int t = i * 6;
                tris[t] = i0; tris[t + 1] = i1; tris[t + 2] = i3;
                tris[t + 3] = i0; tris[t + 4] = i3; tris[t + 5] = i2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            Finish(mesh);
            return mesh;
        }

        static void Finish(Mesh mesh)
        {
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
        }
    }
}
