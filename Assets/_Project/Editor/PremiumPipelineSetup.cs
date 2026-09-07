using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UtezHorror.EditorTools
{
    public static class PremiumPipelineSetup
    {
        private const string PcAsset = "Assets/Settings/URP_PC.asset";
        private const string PcRenderer = "Assets/Settings/URP_PC_Renderer.asset";
        private const string PostFeatureName = "PS1 Post";

        [MenuItem("Tools/UtezHorror/Apply Premium HD Render Settings")]
        public static void ApplyPremium()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcAsset);
            if (asset == null) return;

            var so = new SerializedObject(asset);
            
            // Resolución Nativa y filtros modernos
            Set(so, "m_RenderScale", 1.0f);
            Set(so, "m_UpscalingFilter", 0); // 0 = Auto/Linear
            Set(so, "m_MSAA", 4); // Anti-aliasing MSAA 4x
            Set(so, "m_SoftShadowsSupported", true); // Sombras suaves
            
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            // Remover el filtro PS1 Post
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            if (data != null)
            {
                var rendererSo = new SerializedObject(data);
                SerializedProperty features = rendererSo.FindProperty("m_RendererFeatures");
                SerializedProperty map = rendererSo.FindProperty("m_RendererFeatureMap");

                for (int i = features.arraySize - 1; i >= 0; i--)
                {
                    var featureRef = features.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (featureRef != null && featureRef.name == PostFeatureName)
                    {
                        features.DeleteArrayElementAtIndex(i);
                        features.DeleteArrayElementAtIndex(i); // Delete actually sets to null first time, second removes
                        if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                        Object.DestroyImmediate(featureRef, true);
                    }
                }
                
                // Cleanup nulls
                for (int i = features.arraySize - 1; i >= 0; i--)
                {
                    if (features.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        features.DeleteArrayElementAtIndex(i);
                        if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                    }
                }

                rendererSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Premium] Render scale restaurado a 1.0 (HD), MSAA activado, sombras suaves encendidas, y filtro PS1 Post eliminado.");
        }

        private static void Set(SerializedObject so, string property, float value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null) prop.floatValue = value;
        }

        private static void Set(SerializedObject so, string property, int value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null) prop.intValue = value;
        }

        private static void Set(SerializedObject so, string property, bool value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null) prop.boolValue = value;
        }
    }
}
