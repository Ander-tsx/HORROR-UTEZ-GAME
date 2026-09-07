using UnityEditor;
using UnityEngine;
using UtezHorror.Interaction;
using UtezHorror.Utils;
using static UtezHorror.EditorTools.Level.CecadecLayout;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Assembles a door from kit pieces: a frame plus one or two leaves.
    ///
    /// The leaf pieces are modelled with their pivot on the hinge edge, so <see cref="Door"/>
    /// can swing them by rotating the transform directly. A double door's second leaf is
    /// placed facing back along the wall; <see cref="Door"/> flips its swing sign so both
    /// halves open to the same side.
    /// </summary>
    public static class DoorFactory
    {
        public static Door Build(Transform parent, string name, Vector3 centre, Vector2 along,
                                 bool doubleLeaf, bool glass, bool locked)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre;

            float yaw = Mathf.Atan2(-along.y, along.x) * Mathf.Rad2Deg;
            var dir = new Vector3(along.x, 0f, along.y);
            float width = doubleLeaf ? GlassDoorWidth : DoorWidth;

            KitPlacer.Prop(root.transform, glass ? "Door_Frame_Glass" : "Door_Frame_Single",
                           Vector3.zero, yaw, GameLayers.Environment, collider: false, name: "Frame");

            string leafPiece = glass ? "Door_Leaf_Glass" : "Door_Leaf_Single";
            float leafWidth = doubleLeaf ? width * 0.5f : width;

            Transform[] leaves = doubleLeaf
                ? new[]
                {
                    Hinge(root.transform, leafPiece, "LeafA", -dir * (width * 0.5f), yaw, leafWidth),
                    Hinge(root.transform, leafPiece, "LeafB", dir * (width * 0.5f), yaw + 180f, leafWidth)
                }
                : new[] { Hinge(root.transform, leafPiece, "Leaf", -dir * (width * 0.5f), yaw, leafWidth) };

            var door = root.AddComponent<Door>();
            var so = new SerializedObject(door);
            SerializedProperty list = so.FindProperty("leaves");
            list.arraySize = leaves.Length;
            for (int i = 0; i < leaves.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = leaves[i];

            so.FindProperty("locked").boolValue = locked;
            if (locked) so.FindProperty("lockedPrompt").stringValue = "Cerrado — sin acceso";
            so.ApplyModifiedPropertiesWithoutUndo();

            return door;
        }

        /// <summary>
        /// Builds the hinge as an empty and hangs the leaf inside it, offset by half its width.
        ///
        /// The leaf piece is modelled centred rather than hinged at an edge, because the .blend
        /// importer mirrors X — an edge pivot puts the panel on the wrong side of its hinge.
        /// Owning the offset here keeps the swing correct regardless of how the mesh arrives.
        /// </summary>
        private static Transform Hinge(Transform parent, string piece, string name,
                                       Vector3 hingeOffset, float yaw, float leafWidth)
        {
            var hinge = new GameObject(name);
            hinge.transform.SetParent(parent, false);
            hinge.transform.SetLocalPositionAndRotation(hingeOffset, Quaternion.Euler(0f, yaw, 0f));

            GameObject leaf = KitPlacer.Prop(hinge.transform, piece,
                                             new Vector3(leafWidth * 0.5f, 0f, 0f), 0f,
                                             GameLayers.Interactable, collider: true, name: "Panel");

            // Leaves move, so nothing under the hinge can be batched as static geometry.
            foreach (Transform t in hinge.GetComponentsInChildren<Transform>())
                t.gameObject.isStatic = false;

            return hinge.transform;
        }

    }
}
