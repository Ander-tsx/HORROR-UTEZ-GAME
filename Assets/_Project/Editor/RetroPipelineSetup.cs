using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Applies the PS1-era render settings to both URP tiers.
    ///
    /// Kept in a script rather than tweaked in the inspector for the same reason as
    /// <see cref="SceneLightingSetup"/>: these values look like mistakes to anyone who does not
    /// know the art direction (no anti-aliasing, a quarter-resolution render target), so they
    /// are exactly the settings someone "fixes" back to Unity's defaults. Re-running this menu
    /// item restores them, and <c>RenderPipelineTests</c> fails if they drift.
    ///
    /// See Docs/Plans/02-estilo-visual-ps1.md for why each number is what it is.
    /// </summary>
    public static class RetroPipelineSetup
    {
        /// <summary>
        /// Fraction of the window height the 3D scene is actually rendered at, before being
        /// blown back up with nearest-neighbour filtering. 0.33 of a 1080p window is ~356 lines,
        /// which is the agreed 360p target. The window itself stays at native resolution.
        /// </summary>
        public const float RenderScale = 0.33f;

        /// <summary>UpscalingFilterSelection.Point. Nearest-neighbour is what makes the pixel edge hard.</summary>
        public const int UpscalingPoint = 2;

        private const string PcAsset = "Assets/Settings/URP_PC.asset";
        private const string MobileAsset = "Assets/Settings/URP_Mobile.asset";

        private const string PcRenderer = "Assets/Settings/URP_PC_Renderer.asset";
        private const string MobileRenderer = "Assets/Settings/URP_Mobile_Renderer.asset";
        private const string PostMaterialPath = "Assets/_Project/Art/Materials/Mat_PS1_Post.mat";
        private const string PostFeatureName = "PS1 Post";

        [MenuItem("Tools/UtezHorror/Apply PS1 Render Settings")]
        public static void Apply()
        {
            ApplyTo(PcAsset, shadowResolution: 1024);
            ApplyTo(MobileAsset, shadowResolution: 512);

            Material post = EnsurePostMaterial();
            AddPostFeature(PcRenderer, post);
            AddPostFeature(MobileRenderer, post);

            AssetDatabase.SaveAssets();

            Debug.Log($"[PS1] Render scale {RenderScale:0.00} (~{Mathf.RoundToInt(1080f * RenderScale)}p " +
                      "on a 1080p window), nearest-neighbour upscale, MSAA off, hard shadows.");
        }

        /// <summary>
        /// The whole retro pass in one go: settings, textures, then a level rebuild so the
        /// scene actually picks the new textures up. Regenerating textures without rebuilding
        /// leaves the saved scene pointing at the old material values, which looks like the
        /// change did nothing.
        /// </summary>
        [MenuItem("Tools/UtezHorror/Apply PS1 Look and Rebuild Level")]
        public static void ApplyAndRebuild()
        {
            Apply();
            Level.LevelTextures.RegenerateAll();
            Level.CecadecLevelBuilder.Build();
        }

        /// <summary>
        /// The material the full-screen dither/quantise pass runs. Created rather than committed
        /// as a hand-made asset so the whole look can be restored from one menu item.
        /// </summary>
        private static Material EnsurePostMaterial()
        {
            Shader shader = Shader.Find("UtezHorror/PS1 Post");
            if (shader == null)
            {
                Debug.LogError("[PS1] Shader 'UtezHorror/PS1 Post' not found; the dither pass will be skipped.");
                return null;
            }

            Level.LevelPrimitives.EnsureFolder("Assets/_Project/Art/Materials");

            var material = AssetDatabase.LoadAssetAtPath<Material>(PostMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PostMaterialPath);
            }
            material.shader = shader;

            // 32 levels per channel is the PS1's 5-bit framebuffer. The dither is what turns the
            // banding that causes into a stipple instead of visible steps.
            material.SetFloat("_ColorLevels", 32f);
            material.SetFloat("_DitherStrength", 0.8f);
            material.SetFloat("_VignetteStrength", 0.45f);
            material.SetFloat("_VignettePower", 2.6f);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Attaches URP's own <see cref="FullScreenPassRendererFeature"/> to a renderer.
        ///
        /// Deliberately not a custom ScriptableRendererFeature: in URP 17 that means writing
        /// RenderGraph code, which is the fragile part of a custom post effect. The built-in
        /// feature does the plumbing and all this project has to own is a shader.
        ///
        /// Injected after post-processing, which in URP still runs at the render scale — so the
        /// dither lands on the low-resolution pixel grid. Run it after the upscale instead and
        /// the pattern is magnified into visible 3x3 blocks.
        /// </summary>
        private static void AddPostFeature(string rendererPath, Material material)
        {
            if (material == null) return;

            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (data == null)
            {
                Debug.LogError($"[PS1] Renderer data not found at {rendererPath}.");
                return;
            }

            // Rebuilding from scratch rather than editing in place: re-running the menu item must
            // not stack a second copy of the pass on top of the first.
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(rendererPath).ToArray())
                if (sub is FullScreenPassRendererFeature old && old.name == PostFeatureName)
                    Object.DestroyImmediate(old, allowDestroyingAssets: true);

            var so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");

            for (int i = features.arraySize - 1; i >= 0; i--)
                if (features.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    features.DeleteArrayElementAtIndex(i);
                    if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                }

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = PostFeatureName;
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.passIndex = 0;
            feature.SetActive(true);

            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            // URP keys features by their local file id, so the map has to be extended in step
            // with the list or the renderer drops the feature on the next reload.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            map.arraySize = features.arraySize;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        private static void ApplyTo(string path, int shadowResolution)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[PS1] URP asset not found at {path}.");
                return;
            }

            var so = new SerializedObject(asset);

            // The look.
            Set(so, "m_RenderScale", RenderScale);
            Set(so, "m_UpscalingFilter", UpscalingPoint);

            // Anti-aliasing is the opposite of the point of this. Both of these smooth exactly
            // the pixel staircase the style is built on — and dropping them is a straight
            // performance win on top.
            Set(so, "m_MSAA", 1);
            Set(so, "m_FsrOverrideSharpness", false);

            // A PS1 had no soft shadow filtering. Small, hard shadow maps also cost less, which
            // matters because the torch is a shadow-casting spot light that is on most of the run.
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_MainLightShadowmapResolution", shadowResolution);
            Set(so, "m_AdditionalLightsShadowmapResolution", shadowResolution);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void Set(SerializedObject so, string property, float value)
        {
            SerializedProperty prop = Find(so, property);
            if (prop != null) prop.floatValue = value;
        }

        private static void Set(SerializedObject so, string property, int value)
        {
            SerializedProperty prop = Find(so, property);
            if (prop != null) prop.intValue = value;
        }

        private static void Set(SerializedObject so, string property, bool value)
        {
            SerializedProperty prop = Find(so, property);
            if (prop != null) prop.boolValue = value;
        }

        private static SerializedProperty Find(SerializedObject so, string property)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
                Debug.LogError($"[PS1] '{property}' not found on {so.targetObject.name}. " +
                               "URP probably renamed it; check UniversalRenderPipelineAsset.");
            return prop;
        }
    }
}
