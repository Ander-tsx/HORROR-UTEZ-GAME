using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Builds box meshes at their real size with world-scaled UVs, and keeps them in one
    /// shared asset.
    ///
    /// Why this exists: the level used unit cubes stretched by <c>localScale</c>. A tiling
    /// texture on those stretches with the object — the same floor tile would read 37 m long
    /// on a corridor slab and 1 m on a desk. Baking the size into the mesh and setting UVs in
    /// metres gives constant texel density everywhere, which is the whole basis of texturing
    /// architecture. It also means no non-uniform scale, so normals and physics stay correct.
    ///
    /// Meshes are cached by (size, tiling), so the hundreds of boxes in the level resolve to a
    /// few dozen shared meshes that batch well.
    /// </summary>
    public static class BoxMeshLibrary
    {
        private const string AssetPath = "Assets/_Project/Art/Models/Generated/LevelBoxes.asset";

        /// <summary>
        /// Largest quad, in metres, before a face is split into a grid.
        ///
        /// Nothing to do with lighting or detail: the PS1 shader needs it. A 24-vertex box does
        /// not *wobble* when its vertices snap, it slides bodily across the screen, which reads
        /// as a camera bug rather than as a style. And affine texture mapping shears worse the
        /// larger the triangle, so a 37 m untessellated floor warps hard enough to be unpleasant.
        /// Splitting on a ~1.5 m grid fixes both — the same reason PS1 developers subdivided
        /// their floors by hand. Draw calls are unchanged, only vertex count, which is trivial
        /// here (a level of boxes, not a character).
        /// </summary>
        private const float MaxQuadSize = 1.5f;

        private static readonly Dictionary<(Vector3 size, float tiling), Mesh> Cache = new();
        private static Mesh container;

        /// <summary>Starts a fresh library. Call once at the top of a level build.</summary>
        public static void Begin()
        {
            Cache.Clear();
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Models/Generated");

            // Meshes referenced by a scene must be assets, or the reference dies on reload.
            // One container asset holds them all as sub-assets.
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                if (sub is Mesh) Object.DestroyImmediate(sub, allowDestroyingAssets: true);

            container = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
            if (container == null)
            {
                container = new Mesh { name = "LevelBoxes" };
                AssetDatabase.CreateAsset(container, AssetPath);
            }
        }

        public static void End()
        {
            EditorUtility.SetDirty(container);
            AssetDatabase.SaveAssets();
        }

        /// <param name="size">Real dimensions in metres.</param>
        /// <param name="tiling">Metres per texture repeat. 2 means the texture repeats every 2 m.</param>
        public static Mesh Get(Vector3 size, float tiling)
        {
            var key = (Round(size), Mathf.Round(tiling * 100f) / 100f);
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            Mesh mesh = Build(key.Item1, key.Item2);
            Cache[key] = mesh;

            AssetDatabase.AddObjectToAsset(mesh, container);
            return mesh;
        }

        private static Vector3 Round(Vector3 v) => new(
            Mathf.Round(v.x * 1000f) / 1000f,
            Mathf.Round(v.y * 1000f) / 1000f,
            Mathf.Round(v.z * 1000f) / 1000f);

        private static Mesh Build(Vector3 size, float tiling)
        {
            Vector3 h = size * 0.5f;
            float inv = tiling <= 0f ? 1f : 1f / tiling;

            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var triangles = new List<int>(144);

            // Each face maps its own two world dimensions, so a texture keeps the same
            // physical size no matter which face or which box it lands on.
            AddFace(Vector3.right,   new Vector3(0f, 0f, -1f), Vector3.up,      h.x, size.z, size.y);
            AddFace(Vector3.left,    new Vector3(0f, 0f, 1f),  Vector3.up,      h.x, size.z, size.y);
            AddFace(Vector3.up,      Vector3.right,            new Vector3(0f, 0f, -1f), h.y, size.x, size.z);
            AddFace(Vector3.down,    Vector3.right,            new Vector3(0f, 0f, 1f),  h.y, size.x, size.z);
            AddFace(Vector3.forward, Vector3.right,            Vector3.up,      h.z, size.x, size.y);
            AddFace(Vector3.back,    Vector3.left,             Vector3.up,      h.z, size.x, size.y);

            void AddFace(Vector3 normal, Vector3 right, Vector3 up, float distance, float width, float height)
            {
                Vector3 centre = normal * distance;
                Vector3 r = right * (width * 0.5f);
                Vector3 u = up * (height * 0.5f);

                // Grid resolution, at least 1x1 so a small face stays a single quad.
                int cols = Mathf.Max(1, Mathf.CeilToInt(width / MaxQuadSize));
                int rows = Mathf.Max(1, Mathf.CeilToInt(height / MaxQuadSize));

                int start = vertices.Count;
                float tu = width * inv;
                float tv = height * inv;

                for (int row = 0; row <= rows; row++)
                for (int col = 0; col <= cols; col++)
                {
                    float fx = col / (float)cols;   // 0..1 across the face
                    float fy = row / (float)rows;

                    vertices.Add(centre + r * (fx * 2f - 1f) + u * (fy * 2f - 1f));
                    normals.Add(normal);
                    uvs.Add(new Vector2(fx * tu, fy * tv));
                }

                int stride = cols + 1;
                for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    int a = start + row * stride + col;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    // Winding matches the single-quad version this replaced.
                    triangles.AddRange(new[] { a, d, b, a, c, d });
                }
            }

            var mesh = new Mesh
            {
                name = $"Box_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}_t{tiling:0.##}"
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
