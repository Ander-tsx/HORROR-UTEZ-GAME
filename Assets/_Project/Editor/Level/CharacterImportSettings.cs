using UnityEditor;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Import rules for rigged characters, applied automatically like the kit's.
    ///
    /// The rig is imported as **Humanoid** even though nothing in the project plays an animation
    /// clip yet. That is the point: Humanoid is Unity's retargeting layer, so the day a Mixamo
    /// walk cycle is dropped in it maps onto this skeleton with no rework. Importing as Generic
    /// would work today and close that door.
    ///
    /// The bones are named to Unity's convention by <c>Tools/Blender/make_enemy.py</c>, which is
    /// what lets the avatar be built without a hand-made bone mapping.
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        public const string CharacterFolder = "Assets/_Project/Art/Models/Characters";

        [MenuItem("Tools/UtezHorror/Reimport Characters")]
        public static void ReimportAll()
        {
            AssetDatabase.ImportAsset(CharacterFolder, ImportAssetOptions.ImportRecursive |
                                                        ImportAssetOptions.ForceUpdate);
            Debug.Log("[Characters] Reimported.");
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(CharacterFolder)) return;

            var importer = (ModelImporter)assetImporter;

            // Same trap as the kit: Unity imports a .blend by having Blender export FBX, and that
            // export already converts Z-up to Y-up. Baking it again lays the character on its side.
            importer.bakeAxisConversion = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;   // no normal maps in this project

            // Four influences is more than a PS1 ever had and is plenty for a creature this size;
            // it also keeps skinning cheap on mobile.
            importer.maxBonesPerVertex = 4;
            importer.skinWeights = ModelImporterSkinWeights.Custom;

            importer.materialImportMode = ModelImporterMaterialImportMode.None;   // the level assigns its own
            importer.addCollider = false;
            importer.isReadable = false;
        }
    }
}
