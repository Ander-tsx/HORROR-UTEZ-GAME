using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>A hole in a wall, measured along that wall from its start point.</summary>
    public readonly struct Opening
    {
        public readonly float Centre;
        public readonly float Width;
        public readonly float Height;

        public Opening(float centre, float width, float height)
        {
            Centre = centre;
            Width = width;
            Height = height;
        }

        public float Start => Centre - Width * 0.5f;
        public float End => Centre + Width * 0.5f;
    }

    /// <summary>
    /// Box-and-wall construction kit for the level builder. Everything is axis-aligned boxes
    /// with <see cref="BoxCollider"/>s — no imported meshes, so none of the FBX pivot, axis
    /// and cook-tolerance problems that broke the previous build can occur here.
    /// </summary>
    public static class LevelPrimitives
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Level";

        /// <summary>Default metres per texture repeat. Overridable per surface.</summary>
        public const float DefaultTiling = 2f;

        private static readonly Dictionary<string, Material> Cache = new();

        public static void ResetCache() => Cache.Clear();

        // ---------------------------------------------------------------- materials

        /// <summary>Every level material runs the project's PS1 shader; see Docs/Plans/02.</summary>
        public const string ShaderName = "UtezHorror/PS1 Lit";

        public static Material Mat(string name, Color color, float smoothness = 0.1f,
                                   float metallic = 0f, bool transparent = false,
                                   Color? emission = null, Texture2D baseMap = null,
                                   bool triplanar = false,
                                   float triplanarScale = LevelTextures.MetresPerRepeat)
        {
            if (Cache.TryGetValue(name, out Material cached)) return cached;

            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/Mat_{name}.mat";

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Level] Shader '{ShaderName}' not found; falling back to URP Lit. " +
                               "The level will render without vertex snapping or texture warping.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            // Existing assets were authored against URP/Lit, so the shader has to be reassigned
            // rather than only set at creation, or a rebuild silently keeps the old look.
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            // UVs are in world metres, so the material must not add tiling of its own.
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetTextureScale("_BaseMap", Vector2.one);

            // Triplanar projects from world space and ignores UVs entirely. It is what makes the
            // Blender kit pieces texturable at all: they have no UVs authored for tiling, and
            // hand-unwrapping 31 pieces would have to be redone for every new piece.
            // One shared calibration for the distortion; see PS1Look for why it is not per-material.
            PS1Look.Apply(mat);

            mat.SetFloat("_Triplanar", triplanar ? 1f : 0f);
            mat.SetFloat("_TriplanarScale", triplanarScale);
            if (triplanar) mat.EnableKeyword("_TRIPLANAR");
            else mat.DisableKeyword("_TRIPLANAR");

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>
        /// Axis-aligned box. <paramref name="centre"/> is the centre of the volume.
        ///
        /// The size is baked into the mesh rather than applied as scale, so UVs stay in world
        /// metres and a tiling texture keeps the same physical size on every surface. That is
        /// what makes texturing possible at all; a stretched unit cube cannot be textured.
        /// </summary>
        public static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size,
                                     Material material, int layer, bool collider = true,
                                     float tiling = DefaultTiling)
        {
            var go = new GameObject(name) { layer = layer, isStatic = true };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = BoxMeshLibrary.Get(size, tiling);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            // Auto-fits the mesh bounds, which are now the real dimensions.
            if (collider) go.AddComponent<BoxCollider>();

            return go;
        }

        /// <summary>Box positioned by its footprint rather than its centre — the common case for furniture.</summary>
        public static GameObject BoxOnFloor(Transform parent, string name, Vector3 floorCentre,
                                            Vector3 size, Material material, int layer,
                                            bool collider = true, float tiling = DefaultTiling)
        {
            var centre = floorCentre + new Vector3(0f, size.y * 0.5f, 0f);
            return Box(parent, name, centre, size, material, layer, collider, tiling);
        }

        /// <summary>
        /// A wall running from <paramref name="from"/> to <paramref name="to"/> on the XZ plane,
        /// split into solid segments around each opening, with a lintel above every one.
        /// Openings are given as distance along the wall from <paramref name="from"/>.
        /// </summary>
        public static GameObject Wall(Transform parent, string name, Vector2 from, Vector2 to,
                                      float thickness, float height, Material material, int layer,
                                      IList<Opening> openings = null, float tiling = DefaultTiling)
        {
            var root = new GameObject(name) { isStatic = true };
            root.transform.SetParent(parent, false);

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return root;

            Vector2 dir = delta / length;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            // Local, not world: a whole floor is duplicated by reparenting it under a
            // transform offset in Y, which only works if nothing pins itself to world space.
            root.transform.localPosition = new Vector3(from.x, 0f, from.y);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Inside the root the wall runs along +Z, so segments are placed by distance only.
            var sorted = new List<Opening>(openings ?? new List<Opening>());
            sorted.Sort((a, b) => a.Centre.CompareTo(b.Centre));

            float cursor = 0f;
            int index = 0;
            foreach (Opening opening in sorted)
            {
                float start = Mathf.Clamp(opening.Start, 0f, length);
                float end = Mathf.Clamp(opening.End, 0f, length);

                if (start > cursor)
                    Segment(root.transform, $"Seg{index++}", cursor, start, thickness, height, material, layer, tiling);

                if (opening.Height < height)
                    Lintel(root.transform, $"Lintel{index++}", start, end, thickness,
                           opening.Height, height, material, layer, tiling);

                cursor = Mathf.Max(cursor, end);
            }

            if (cursor < length)
                Segment(root.transform, $"Seg{index}", cursor, length, thickness, height, material, layer, tiling);

            return root;
        }

        private static void Segment(Transform parent, string name, float a, float b,
                                    float thickness, float height, Material material, int layer,
                                    float tiling)
        {
            float len = b - a;
            if (len <= 0.01f) return;
            Box(parent, name, new Vector3(0f, height * 0.5f, a + len * 0.5f),
                new Vector3(thickness, height, len), material, layer, collider: true, tiling);
        }

        private static void Lintel(Transform parent, string name, float a, float b, float thickness,
                                   float bottom, float top, Material material, int layer, float tiling)
        {
            float len = b - a;
            float h = top - bottom;
            if (len <= 0.01f || h <= 0.01f) return;
            Box(parent, name, new Vector3(0f, bottom + h * 0.5f, a + len * 0.5f),
                new Vector3(thickness, h, len), material, layer, collider: true, tiling);
        }

        /// <summary>Rectangular slab (floor or ceiling) spanning a min/max XZ rectangle.</summary>
        public static GameObject Slab(Transform parent, string name, Vector2 min, Vector2 max,
                                      float y, float thickness, Material material, int layer,
                                      float tiling = DefaultTiling)
        {
            var size = new Vector3(max.x - min.x, thickness, max.y - min.y);
            var centre = new Vector3((min.x + max.x) * 0.5f, y + thickness * 0.5f, (min.y + max.y) * 0.5f);
            return Box(parent, name, centre, size, material, layer, collider: true, tiling);
        }

        public static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name) { isStatic = true };
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
