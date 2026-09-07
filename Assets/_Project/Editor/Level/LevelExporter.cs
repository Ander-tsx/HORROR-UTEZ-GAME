using System.IO;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Exports the generated level to FBX so it can be opened in Blender.
    ///
    /// The FBX is an **export, not the source**. <see cref="CecadecLayout"/> plus the builder
    /// remain the source of truth, and rebuilding overwrites the scene. Anything hand-modelled
    /// in Blender comes back as art that replaces a specific piece — it does not round-trip
    /// into the builder.
    /// </summary>
    public static class LevelExporter
    {
        private const string OutputFolder = "Export";

        [MenuItem("Tools/UtezHorror/Export Level to FBX (for Blender)")]
        public static void ExportLevel()
        {
            if (!SceneManager.GetActiveScene().path.EndsWith("Cecadec.unity"))
                EditorSceneManager.OpenScene(CecadecLevelBuilder.ScenePath, OpenSceneMode.Single);

            var root = GameObject.Find("Cecadec");
            if (root == null)
            {
                Debug.LogError("[Export] No 'Cecadec' root in the scene. Build the level first.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            string path = Path.GetFullPath(Path.Combine(OutputFolder, "Cecadec_Level.fbx"));

            var options = new ExportModelOptions
            {
                // Blender refuses ASCII FBX outright ("ASCII FBX files are not supported"),
                // and ASCII is this exporter's default. This one line is the whole reason
                // the earlier export could not be opened.
                ExportFormat = ExportFormat.Binary,
                ObjectPosition = ObjectPosition.WorldAbsolute,
                ExportUnrendered = true,
                KeepInstances = false   // instanced boxes come through as real meshes to edit
            };

            string written = ModelExporter.ExportObject(path, root, options);
            if (string.IsNullOrEmpty(written))
            {
                Debug.LogError("[Export] The FBX exporter returned nothing; the file was not written.");
                return;
            }

            Debug.Log($"[Export] Wrote {written}\n" +
                      "Blender: File > Import > FBX. Keep the default Forward -Z / Up Y so the " +
                      "building lands the right way up.");
        }
    }
}
