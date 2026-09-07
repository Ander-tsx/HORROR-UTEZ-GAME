using System.Collections.Generic;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    public enum KitOpeningKind { Entrance, Door, GlassDoor }

    /// <summary>An opening in a wall, positioned by its centre measured along that wall.</summary>
    public readonly struct KitOpening
    {
        public readonly float Centre;
        public readonly KitOpeningKind Kind;

        public KitOpening(float centre, KitOpeningKind kind)
        {
            Centre = centre;
            Kind = kind;
        }
    }

    /// <summary>
    /// Assembles walls out of kit modules.
    ///
    /// Runs are composed greedily from 4 m, 2 m and 1 m pieces, and openings are 4 m modules
    /// with the hole already in them. This is why the layout is snapped to whole metres: a
    /// run that is not a whole number of modules cannot be filled without stretching a piece,
    /// and a stretched piece breaks texel density — the thing that keeps a level looking
    /// consistent.
    /// </summary>
    public static class KitPlacer
    {
        /// <summary>Width of every opening module, and the granularity openings must sit on.</summary>
        public const float OpeningModule = 4f;

        private static readonly float[] SolidModules = { 4f, 2f, 1f };

        public static GameObject Wall(Transform parent, string name, Vector2 from, Vector2 to,
                                      bool exterior, int layer, IList<KitOpening> openings = null)
        {
            var root = LevelPrimitives.Group(parent, name);

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return root;

            Vector2 dir = delta / length;
            // Rotating by this yaw sends the piece's local +X along the wall.
            float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

            var sorted = new List<KitOpening>(openings ?? new List<KitOpening>());
            sorted.Sort((a, b) => a.Centre.CompareTo(b.Centre));

            float cursor = 0f;
            int index = 0;

            foreach (KitOpening opening in sorted)
            {
                float start = opening.Centre - OpeningModule * 0.5f;
                FillSolid(root.transform, from, dir, yaw, cursor, start, exterior, layer, ref index);

                Place(root.transform, OpeningPiece(opening.Kind, exterior), from, dir, yaw,
                      opening.Centre, layer, ref index);

                cursor = opening.Centre + OpeningModule * 0.5f;
            }

            FillSolid(root.transform, from, dir, yaw, cursor, length, exterior, layer, ref index);
            return root;
        }

        private static string OpeningPiece(KitOpeningKind kind, bool exterior) => kind switch
        {
            KitOpeningKind.Entrance => "Wall_Ext_Entrance",
            KitOpeningKind.GlassDoor => "Wall_Int_GlassDoor",
            _ => exterior ? "Wall_Ext_Door" : "Wall_Int_Door"
        };

        private static void FillSolid(Transform parent, Vector2 from, Vector2 dir, float yaw,
                                      float start, float end, bool exterior, int layer, ref int index)
        {
            float remaining = end - start;
            if (remaining <= 0.01f) return;

            float cursor = start;
            foreach (float module in SolidModules)
            {
                while (remaining >= module - 0.01f)
                {
                    string piece = exterior ? $"Wall_Ext_{module:0}m" : $"Wall_Int_{module:0}m";
                    Place(parent, piece, from, dir, yaw, cursor + module * 0.5f, layer, ref index);
                    cursor += module;
                    remaining -= module;
                }
            }

            if (remaining > 0.01f)
                Debug.LogError($"[Kit] {remaining:0.00} m of wall left over between {start:0.00} and " +
                               $"{end:0.00}. The layout must be a whole number of metres.");
        }

        private static void Place(Transform parent, string piece, Vector2 from, Vector2 dir,
                                  float yaw, float distance, int layer, ref int index)
        {
            Vector2 p = from + dir * distance;
            KitLibrary.Place(parent, piece, new Vector3(p.x, 0f, p.y), yaw, layer,
                             name: $"{piece}_{index++}");
        }

        /// <summary>Places a prop from the kit, positioned on the XZ plane at a given height.</summary>
        public static GameObject Prop(Transform parent, string piece, Vector3 position, float yaw,
                                      int layer, bool collider = true, string name = null) =>
            KitLibrary.Place(parent, piece, position, yaw, layer, collider, name);
    }
}
