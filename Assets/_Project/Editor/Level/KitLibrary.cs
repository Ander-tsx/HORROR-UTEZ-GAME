using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Loads the modular kit pieces by name. Every piece is a .blend under
    /// <see cref="KitImportSettings.KitFolder"/>; edit one in Blender, save, and the level
    /// picks it up the next time it is built.
    /// </summary>
    public static class KitLibrary
    {
        private static readonly Dictionary<string, GameObject> Cache = new();
        private static readonly HashSet<string> Missing = new();

        public static void ResetCache()
        {
            Cache.Clear();
            Missing.Clear();
            KitMaterials.ResetCache();
        }

        public static GameObject Prefab(string pieceName)
        {
            if (Cache.TryGetValue(pieceName, out GameObject cached)) return cached;

            string path = $"{KitImportSettings.KitFolder}/{pieceName}.blend";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (asset == null && Missing.Add(pieceName))
                Debug.LogError($"[Kit] Missing piece '{pieceName}' at {path}. " +
                               "Regenerate with Tools/Blender/generate_kit.py.");

            Cache[pieceName] = asset;
            return asset;
        }

        /// <summary>
        /// Instantiates a piece. <paramref name="position"/> is in the parent's local space and
        /// lands on the piece's own pivot, which is documented in generate_kit.py.
        /// </summary>
        public static GameObject Place(Transform parent, string pieceName, Vector3 position,
                                       float yaw, int layer, bool collider = true,
                                       string name = null)
        {
            GameObject prefab = Prefab(pieceName);
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name ?? pieceName;

            // The importer leaves a -90° X rotation on the root to turn Blender's Z-up into
            // Unity's Y-up. Compose the yaw on top of it instead of replacing it, or every
            // piece lands on its side.
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * instance.transform.localRotation;

            SetLayerRecursively(instance.transform, layer);
            if (collider) AddCollidersTo(instance);
            // Without this the piece keeps the flat, untextured material it was given in
            // Blender, which is what left every wall in the building untextured.
            KitMaterials.Remap(instance);
            MarkStatic(instance);

            return instance;
        }

        /// <summary>
        /// Box colliders sized from each renderer's mesh. Cheap and exact for a kit made of
        /// boxes, and it avoids the MeshCollider cooking problems the FBX pipeline used to hit.
        /// </summary>
        private static void AddCollidersTo(GameObject instance)
        {
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var box = filter.gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursively(child, layer);
        }

        private static void MarkStatic(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
                t.gameObject.isStatic = true;
        }
    }
}
