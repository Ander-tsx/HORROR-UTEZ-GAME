using System.IO;
using UnityEditor;
using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Creates the configuration assets the scene builders expect. Idempotent: safe to run
    /// on a fresh clone or after someone deletes an asset by accident.
    /// </summary>
    public static class ProjectAssetsBootstrapper
    {
        private const string SoFolder = "Assets/_Project/ScriptableObjects";

        [MenuItem("Tools/UtezHorror/Create Default Assets")]
        public static void CreateDefaults()
        {
            EnsureFolder(SoFolder);
            EnsureAsset<ShiftConfig>($"{SoFolder}/ShiftConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void EnsureAsset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                Debug.Log($"[Bootstrap] {path} already exists.");
                return;
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            Debug.Log($"[Bootstrap] Created {path}.");
        }
    }
}
