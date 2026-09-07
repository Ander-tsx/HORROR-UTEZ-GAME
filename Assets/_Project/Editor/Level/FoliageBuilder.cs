using UnityEditor;
using UnityEngine;
using UtezHorror.Utils;
using Random = System.Random;
using static UtezHorror.EditorTools.Level.ExteriorLayout;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Scatters the wood between the two buildings.
    ///
    /// A tree here is three crossed quads with a cut-out canopy texture and a small box trunk —
    /// which is exactly what a PS1 tree was, and still the only thing that carries a wood on a
    /// phone. The leaves move by vertex animation in <c>PS1_Foliage</c>, so the wind costs
    /// nothing per frame on the CPU.
    ///
    /// Draw distance is the fog's job, as it was in 1997. That is also what makes the crossing
    /// frightening: the wood has no far edge, only the point where it stops resolving.
    /// </summary>
    public static class FoliageBuilder
    {
        private const string MeshAssetPath = "Assets/_Project/Art/Models/Generated/Foliage.asset";
        private const int Seed = 20260906;

        /// <summary>Canopy quads per tree. Three at 60° reads as round from every angle.</summary>
        private const int CrossQuads = 3;

        public static void Build(Transform root)
        {
            Material canopy = LevelPrimitives.Mat("TreeCanopy", new Color(0.62f, 0.70f, 0.55f), 0.02f,
                                                  baseMap: LevelTextures.Canopy);
            ConfigureFoliageMaterial(canopy);

            Material bark = LevelPrimitives.Mat("TreeBark", new Color(0.70f, 0.66f, 0.62f), 0.03f,
                                                baseMap: LevelTextures.Bark,
                                                triplanar: true, triplanarScale: 1.2f);

            Mesh canopyMesh = BuildCanopyMesh();
            SaveMesh(canopyMesh);

            var wood = LevelPrimitives.Group(root, "Wood");
            var rng = new Random(Seed);
            int planted = 0;

            for (float x = WorldMin.x; x < WorldMax.x; x += TreeSpacing)
            for (float z = WorldMin.y; z < WorldMax.y; z += TreeSpacing)
            {
                float jx = x + (float)(rng.NextDouble() - 0.5) * 2f * TreeJitter;
                float jz = z + (float)(rng.NextDouble() - 0.5) * 2f * TreeJitter;

                if (!IsPlantable(new Vector2(jx, jz))) continue;
                // Gaps are as important as trees: an evenly packed wood reads as wallpaper and
                // gives the player nowhere to see anything coming.
                if (rng.NextDouble() < 0.22) continue;

                Plant(wood.transform, planted++, new Vector3(jx, TerrainBuilder.HeightAt(jx, jz), jz),
                      canopyMesh, canopy, bark, rng);
            }

            Debug.Log($"[Exterior] Planted {planted} trees.");
        }

        /// <summary>
        /// Keeps trees off the pads and the path. Without the path clearance the wood closes over
        /// the only route between the buildings, and a player who cannot find their way back is
        /// lost rather than frightened.
        /// </summary>
        private static bool IsPlantable(Vector2 p)
        {
            if (Grow(CecadecPad, TreeClearance).Contains(p)) return false;
            if (Grow(CdsPad, TreeClearance).Contains(p)) return false;
            if (DistanceToPath(p) < PathHalfWidth + TreeClearance) return false;
            return true;
        }

        private static void Plant(Transform parent, int index, Vector3 position, Mesh canopyMesh,
                                  Material canopy, Material bark, Random rng)
        {
            float height = 4.2f + (float)rng.NextDouble() * 3.4f;
            float spread = 0.75f + (float)rng.NextDouble() * 0.45f;
            float yaw = (float)rng.NextDouble() * 360f;

            var tree = new GameObject($"Tree_{index}") { isStatic = false, layer = GameLayers.Environment };
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = new Vector3(spread, height / 6f, spread);

            // Not static: the shader moves its vertices, and static batching would bake them into
            // one combined mesh whose object-space Y no longer means "height up this trunk".

            var leaves = new GameObject("Canopy") { layer = GameLayers.Environment };
            leaves.transform.SetParent(tree.transform, false);
            leaves.AddComponent<MeshFilter>().sharedMesh = canopyMesh;
            var renderer = leaves.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = canopy;
            // A wood of shadow-casting alpha-cut quads costs far more than it adds in a scene
            // this dark; the fog eats anything more than a few metres away regardless.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The trunk is a solid box so the player collides with something believable, and so
            // the torch has an object to pick out of the fog.
            GameObject trunk = LevelPrimitives.BoxOnFloor(tree.transform, "Trunk", Vector3.zero,
                                                          new Vector3(0.34f, 6f, 0.34f), bark,
                                                          GameLayers.Environment, tiling: 1.2f);
            trunk.isStatic = false;
        }

        /// <summary>
        /// <see cref="CrossQuads"/> vertical quads rotated evenly about Y, sharing one mesh across
        /// every tree in the level.
        /// </summary>
        private static Mesh BuildCanopyMesh()
        {
            var vertices = new Vector3[CrossQuads * 4];
            var normals = new Vector3[CrossQuads * 4];
            var uvs = new Vector2[CrossQuads * 4];
            var triangles = new int[CrossQuads * 6];

            const float halfWidth = 3.2f;
            const float bottom = 2.0f;    // object space; the trunk shows below this
            const float top = 8.6f;

            for (int q = 0; q < CrossQuads; q++)
            {
                float angle = 180f / CrossQuads * q;
                Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3 right = rotation * Vector3.right * halfWidth;
                Vector3 normal = rotation * Vector3.forward;

                int v = q * 4;
                vertices[v + 0] = -right + Vector3.up * bottom;
                vertices[v + 1] = right + Vector3.up * bottom;
                vertices[v + 2] = right + Vector3.up * top;
                vertices[v + 3] = -right + Vector3.up * top;

                for (int i = 0; i < 4; i++) normals[v + i] = normal;

                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);

                int t = q * 6;
                triangles[t + 0] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "TreeCanopy" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Swaps the canopy onto the foliage shader and sets its sway.</summary>
        private static void ConfigureFoliageMaterial(Material material)
        {
            Shader shader = Shader.Find("UtezHorror/PS1 Foliage");
            if (shader == null)
            {
                Debug.LogError("[Exterior] Shader 'UtezHorror/PS1 Foliage' not found; trees will not move.");
                return;
            }

            material.shader = shader;
            PS1Look.Apply(material, PS1Look.FoliageAffineAmount);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_SwayStrength", 0.14f);
            material.SetFloat("_SwaySpeed", 1.0f);
            material.SetFloat("_SwayBaseHeight", 2.0f);
            material.SetFloat("_SwayTopHeight", 8.6f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
        }

        private static void SaveMesh(Mesh mesh)
        {
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Models/Generated");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath) != null)
                AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.CreateAsset(mesh, MeshAssetPath);
        }
    }
}
