using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UtezHorror.Utils;
using static UtezHorror.EditorTools.Level.ExteriorLayout;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Generates the outdoor ground as a mesh.
    ///
    /// Deliberately not a Unity Terrain, for three reasons:
    ///   1. A Terrain draws with its own shader, so it could not run the project's PS1 shader —
    ///      the vertex snap and the texture warp would be missing on the single largest surface
    ///      in the game, which would look like a bug rather than a style.
    ///   2. A Terrain is sculpted by hand and stored as an asset. Every other piece of this level
    ///      is generated from a layout, and hand edits are expected not to survive a rebuild;
    ///      a Terrain would quietly break that contract.
    ///   3. Terrain, and its detail/tree system, is expensive on a phone.
    ///
    /// The height field is noise, flattened to a level pad under each building. That flattening
    /// is not cosmetic: an axis-aligned box building standing on rolling ground leaves gaps
    /// underneath that the player falls through.
    /// </summary>
    public static class TerrainBuilder
    {
        private const string MeshAssetPath = "Assets/_Project/Art/Models/Generated/Terrain.asset";

        public static void Build(Transform root)
        {
            Material ground = LevelPrimitives.Mat("GroundEarth", new Color(0.85f, 0.88f, 0.80f), 0.02f,
                                                  baseMap: LevelTextures.Ground,
                                                  triplanar: true, triplanarScale: 3f);

            Mesh mesh = BuildMesh();
            SaveMesh(mesh);

            var go = new GameObject("Terrain") { isStatic = true, layer = GameLayers.Environment };
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ground;

            // The mesh is authored directly in world metres on an identity transform, which is
            // what keeps PhysX's cooking tolerances sane. Baking a collider from a mesh that only
            // reaches its real size through a large transform scale welds vertices that should be
            // separate and punches invisible holes in the floor — that bug cost this project a
            // whole session once already.
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Ground height at a world XZ. Used by the tree scatter, so both agree exactly.</summary>
        public static float HeightAt(float x, float z)
        {
            float ox = TerrainSeed % 1000 * 0.37f;
            float oz = TerrainSeed % 997 * 0.53f;

            float hills = (Mathf.PerlinNoise((x + ox) / HillWavelength, (z + oz) / HillWavelength) - 0.5f)
                          * 2f * HillAmplitude;
            float bumps = (Mathf.PerlinNoise((x + ox) / BumpWavelength, (z + oz) / BumpWavelength) - 0.5f)
                          * 2f * BumpAmplitude;

            float natural = hills + bumps;

            // Flatten under each building and along the path, blending out over PadFalloff so the
            // pad edge reads as a shallow bank rather than a cliff.
            float flat = Mathf.Max(PadWeight(x, z, CecadecPad), PadWeight(x, z, CdsPad));
            flat = Mathf.Max(flat, PathWeight(x, z));

            return Mathf.Lerp(natural, PadHeight, flat);
        }

        /// <summary>1 inside the pad, easing to 0 across <see cref="PadFalloff"/> outside it.</summary>
        private static float PadWeight(float x, float z, Rect pad)
        {
            float dx = Mathf.Max(pad.xMin - x, x - pad.xMax, 0f);
            float dz = Mathf.Max(pad.yMin - z, z - pad.yMax, 0f);
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / PadFalloff));
        }

        private static float PathWeight(float x, float z)
        {
            float distance = DistanceToPath(new Vector2(x, z));
            float outer = PathHalfWidth + 3f;
            if (distance >= outer) return 0f;

            // The path is levelled but only three quarters of the way, so it still rolls with the
            // ground instead of reading as a concrete ribbon laid over a hillside.
            return 0.75f * (1f - Mathf.SmoothStep(PathHalfWidth, outer, distance));
        }

        private static Mesh BuildMesh()
        {
            int cols = Mathf.CeilToInt((WorldMax.x - WorldMin.x) / GridStep);
            int rows = Mathf.CeilToInt((WorldMax.y - WorldMin.y) / GridStep);

            var vertices = new List<Vector3>((cols + 1) * (rows + 1));
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(cols * rows * 6);

            for (int row = 0; row <= rows; row++)
            for (int col = 0; col <= cols; col++)
            {
                float x = WorldMin.x + col * GridStep;
                float z = WorldMin.y + row * GridStep;
                vertices.Add(new Vector3(x, HeightAt(x, z), z));

                // World-metre UVs, matching the level's texel density convention. The material
                // is triplanar anyway, so these exist for anything that later wants them.
                uvs.Add(new Vector2(x / LevelTextures.MetresPerRepeat, z / LevelTextures.MetresPerRepeat));
            }

            int stride = cols + 1;
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                int a = row * stride + col;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;

                triangles.AddRange(new[] { a, c, b, b, c, d });
            }

            var mesh = new Mesh { name = "Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SaveMesh(Mesh mesh)
        {
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Models/Generated");

            // A mesh referenced by a scene has to be an asset, or the reference dies on reload.
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existing != null) AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.CreateAsset(mesh, MeshAssetPath);
        }
    }
}
