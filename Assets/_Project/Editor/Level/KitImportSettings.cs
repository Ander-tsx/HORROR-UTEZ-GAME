using UnityEditor;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Import rules for the modular kit. Applied automatically, so a piece edited and saved in
    /// Blender comes back into the level correctly without anyone remembering to tick boxes.
    /// </summary>
    public sealed class KitImportSettings : AssetPostprocessor
    {
        public const string KitFolder = "Assets/_Project/Art/Models/Kit";

        [MenuItem("Tools/UtezHorror/Reimport Kit")]
        public static void ReimportAll()
        {
            AssetDatabase.ImportAsset(KitFolder, ImportAssetOptions.ImportRecursive |
                                                 ImportAssetOptions.ForceUpdate);
            Debug.Log("[Kit] Reimported every piece.");
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(KitFolder)) return;

            var importer = (ModelImporter)assetImporter;

            // Leave this OFF. Unity imports a .blend by having Blender export FBX, and that
            // export already converts Z-up to Y-up. Switching bake on applies the conversion a
            // second time, which cancels the first: pieces arrive in raw Blender coordinates
            // and every wall lands on its side.
            importer.bakeAxisConversion = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;

            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            // Keep the materials authored in the .blend, so texturing a piece in Blender shows
            // up in Unity with no extra step.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            importer.addCollider = false;   // the builder adds box colliders where they matter
            importer.isReadable = false;
        }
    }
}
