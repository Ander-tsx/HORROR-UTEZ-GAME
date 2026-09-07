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
    /// Primary path: uses the StarkCrafts PSX forest FBX (committed under
    /// Assets/_Project/Art/Models/Environment/Trees/). The FBX contains several
    /// named meshes; anything with "Tree", "Pine" or "Bush" in the name becomes a
    /// planting candidate rotated through at random so the wood is not a clone forest.
    ///
    /// Fallback: if the FBX is not yet imported (first-time setup) the original
    /// procedural cross-quad approach is used so the level can still build.
    ///
    /// Either way the exterior atmosphere is pitch-black with dense fog; AtmospherePresets
    /// enforces that at runtime, and SceneLightingSetup stamps it into the saved scene.
    /// </summary>
    public static class FoliageBuilder
    {
        private const string TreeFbxPath =
            "Assets/_Project/Art/Models/Environment/Trees/PSX_Forest_AssetCollection_byStarkCrafts.fbx";

        /// <summary>Canopy quads per tree — only used in the cross-quad fallback.</summary>
        private const int CrossQuads = 3;

        private const string MeshAssetPath =
            "Assets/_Project/Art/Models/Generated/Foliage.asset";

        private const int Seed = 20260906;

        // ── Public entry point ────────────────────────────────────────────────

        public static void Build(Transform root)
        {
            // Force-import so LoadAllAssetsAtPath picks up sub-assets immediately.
            AssetDatabase.ImportAsset(TreeFbxPath, ImportAssetOptions.Default);
            Mesh[] treeMeshes = LoadTreeMeshes();
            bool   useFbx     = treeMeshes != null && treeMeshes.Length > 0;

            Debug.Log(useFbx
                ? $"[Foliage] Using {treeMeshes.Length} mesh(es) from StarkCrafts FBX."
                : "[Foliage] FBX not found or has no tree meshes — falling back to cross-quads.");

            // Darker green for the premium look; bark is darker/warmer brown.
            Material canopy = LevelPrimitives.Mat(
                "TreeCanopy", new Color(0.22f, 0.35f, 0.20f), 0.04f,
                baseMap: useFbx ? null : LevelTextures.Canopy);

            if (!useFbx) ConfigureFoliageMaterial(canopy);

            Material bark = LevelPrimitives.Mat(
                "TreeBark", new Color(0.35f, 0.28f, 0.22f), 0.07f,
                baseMap: LevelTextures.Bark, triplanar: true, triplanarScale: 1.2f);

            Mesh fallbackMesh = null;
            if (!useFbx)
            {
                fallbackMesh = BuildCanopyMesh();
                SaveMesh(fallbackMesh);
            }

            var wood    = LevelPrimitives.Group(root, "Wood");
            var rng     = new Random(Seed);
            int planted = 0;

            for (float x = WorldMin.x; x < WorldMax.x; x += TreeSpacing)
            for (float z = WorldMin.y; z < WorldMax.y; z += TreeSpacing)
            {
                float jx = x + (float)(rng.NextDouble() - 0.5) * 2f * TreeJitter;
                float jz = z + (float)(rng.NextDouble() - 0.5) * 2f * TreeJitter;

                if (!IsPlantable(new Vector2(jx, jz))) continue;
                // Deliberate gaps — an evenly packed wood reads as wallpaper.
                if (rng.NextDouble() < 0.22) continue;

                Vector3 pos = new Vector3(jx, TerrainBuilder.HeightAt(jx, jz), jz);

                if (useFbx)
                    PlantFbx(wood.transform,  planted++, pos, treeMeshes, canopy, bark, rng);
                else
                    PlantQuad(wood.transform, planted++, pos, fallbackMesh, canopy, bark, rng);
            }

            Debug.Log($"[Exterior] Planted {planted} trees.");
        }

        // ── FBX path ─────────────────────────────────────────────────────────

        private static Mesh[] LoadTreeMeshes()
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(TreeFbxPath);
            var meshes    = new System.Collections.Generic.List<Mesh>();
            foreach (var asset in allAssets)
            {
                if (asset is Mesh m &&
                    (m.name.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     m.name.IndexOf("Pine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     m.name.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    meshes.Add(m);
            }
            return meshes.Count > 0 ? meshes.ToArray() : null;
        }

        private static void PlantFbx(Transform parent, int index, Vector3 position,
                                     Mesh[] meshes, Material canopy, Material bark, Random rng)
        {
            float height   = 4.0f + (float)rng.NextDouble() * 3.0f;
            float spread   = 0.8f + (float)rng.NextDouble() * 0.4f;
            float yaw      = (float)rng.NextDouble() * 360f;
            float fbxScale = height / 6f;

            var tree = new GameObject($"Tree_{index}") { isStatic = false, layer = GameLayers.Environment };
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale    = new Vector3(spread * fbxScale, fbxScale, spread * fbxScale);

            Mesh chosen = meshes[rng.Next(meshes.Length)];

            var leaves = new GameObject("Canopy") { layer = GameLayers.Environment };
            leaves.transform.SetParent(tree.transform, false);
            leaves.AddComponent<MeshFilter>().sharedMesh = chosen;
            var mr = leaves.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = canopy;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // A slim trunk the torch can pick out of the fog.
            var trunk = LevelPrimitives.BoxOnFloor(tree.transform, "Trunk", Vector3.zero,
                                                    new Vector3(0.28f, 6f, 0.28f), bark,
                                                    GameLayers.Environment, tiling: 1.2f);
            trunk.isStatic = false;
        }

        // ── Fallback cross-quad path ──────────────────────────────────────────

        private static void PlantQuad(Transform parent, int index, Vector3 position, Mesh canopyMesh,
                                      Material canopy, Material bark, Random rng)
        {
            float height = 4.2f + (float)rng.NextDouble() * 3.4f;
            float spread = 0.75f + (float)rng.NextDouble() * 0.45f;
            float yaw    = (float)rng.NextDouble() * 360f;

            var tree = new GameObject($"Tree_{index}") { isStatic = false, layer = GameLayers.Environment };
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale    = new Vector3(spread, height / 6f, spread);

            var leaves = new GameObject("Canopy") { layer = GameLayers.Environment };
            leaves.transform.SetParent(tree.transform, false);
            leaves.AddComponent<MeshFilter>().sharedMesh = canopyMesh;
            var renderer = leaves.AddComponent<MeshRenderer>();
            renderer.sharedMaterial    = canopy;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var trunk = LevelPrimitives.BoxOnFloor(tree.transform, "Trunk", Vector3.zero,
                                                    new Vector3(0.34f, 6f, 0.34f), bark,
                                                    GameLayers.Environment, tiling: 1.2f);
            trunk.isStatic = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsPlantable(Vector2 p)
        {
            if (Grow(CecadecPad, TreeClearance).Contains(p)) return false;
            if (Grow(CdsPad, TreeClearance).Contains(p))     return false;
            if (DistanceToPath(p) < PathHalfWidth + TreeClearance) return false;
            return true;
        }

        private static Mesh BuildCanopyMesh()
        {
            var vertices  = new Vector3[CrossQuads * 4];
            var normals   = new Vector3[CrossQuads * 4];
            var uvs       = new Vector2[CrossQuads * 4];
            var triangles = new int[CrossQuads * 6];

            const float halfWidth = 3.2f;
            const float bottom    = 2.0f;
            const float top       = 8.6f;

            for (int q = 0; q < CrossQuads; q++)
            {
                float     angle    = 180f / CrossQuads * q;
                Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3   right    = rotation * Vector3.right * halfWidth;
                Vector3   normal   = rotation * Vector3.forward;

                int v = q * 4;
                vertices[v + 0] = -right + Vector3.up * bottom;
                vertices[v + 1] =  right + Vector3.up * bottom;
                vertices[v + 2] =  right + Vector3.up * top;
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
            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureFoliageMaterial(Material material)
        {
            Shader shader = Shader.Find("UtezHorror/PS1 Foliage");
            if (shader == null)
            {
                Debug.LogError("[Exterior] Shader 'UtezHorror/PS1 Foliage' not found.");
                return;
            }
            material.shader = shader;
            PS1Look.Apply(material, PS1Look.FoliageAffineAmount);
            material.SetFloat("_Cutoff",         0.5f);
            material.SetFloat("_SwayStrength",   0.14f);
            material.SetFloat("_SwaySpeed",      1.0f);
            material.SetFloat("_SwayBaseHeight", 2.0f);
            material.SetFloat("_SwayTopHeight",  8.6f);
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
