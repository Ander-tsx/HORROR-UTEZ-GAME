using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UtezHorror.Utils;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Bakes the walkable surface for both buildings, the stair and the ground between them.
    ///
    /// Baked at level build time rather than by hand in the Navigation window, for the same
    /// reason as everything else here: the scene is regenerated from the layout, so anything
    /// baked by hand is lost on the next rebuild and the enemy silently stops moving.
    ///
    /// It runs *after* the level is built and *before* the enemy is placed. Bake with the enemy
    /// already in the scene and its own collider is carved into the mesh, leaving it standing in
    /// a hole it cannot walk out of.
    /// </summary>
    public static class NavMeshBaker
    {
        private const string DataPath = "Assets/_Project/Art/Models/Generated/Cecadec_NavMesh.asset";

        public static void Bake(GameObject levelRoot)
        {
            var surface = levelRoot.GetComponent<NavMeshSurface>();
            if (surface == null) surface = levelRoot.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;

            // Physics colliders, not render meshes. The level is built from box colliders that
            // are simpler than the geometry they stand for, and the trees are alpha-cut quads
            // whose render mesh would bake into a wall of navigation obstacles.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // Enemies are excluded so an enemy standing in the scene cannot carve itself in.
            surface.layerMask = (1 << GameLayers.Environment)
                              | (1 << GameLayers.Prop)
                              | (1 << GameLayers.Interactable)
                              | 1;   // Default, for anything unassigned

            surface.overrideTileSize = false;
            surface.overrideVoxelSize = false;

            surface.BuildNavMesh();

            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("[NavMesh] Bake produced no data. The enemy will not move.");
                return;
            }

            // The data has to become an asset or the reference dies when the scene reloads,
            // and the enemy comes back to a scene with no navigation at all.
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Models/Generated");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath) != null)
                AssetDatabase.DeleteAsset(DataPath);

            data.name = "Cecadec_NavMesh";
            AssetDatabase.CreateAsset(data, DataPath);
            surface.navMeshData = data;
            EditorUtility.SetDirty(surface);

            Debug.Log($"[NavMesh] Baked {data.sourceBounds.size.x:0} x {data.sourceBounds.size.z:0} m " +
                      "of walkable surface.");
        }
    }
}
