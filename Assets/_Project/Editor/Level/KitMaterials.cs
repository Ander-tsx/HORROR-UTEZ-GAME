using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Swaps the materials a kit piece was authored with in Blender for the level's own.
    ///
    /// Why this exists: <see cref="KitImportSettings"/> imports each .blend with
    /// <c>ImportStandard</c>, so every piece arrives wearing the flat, untextured material that
    /// <c>generate_kit.py</c> gave it. The building is 597 kit instances, so before this class
    /// existed <see cref="LevelTextures.Wall"/> was not applied to a single wall in the level —
    /// the only textured surfaces were the floor and ceiling slabs, which are built from boxes.
    /// That, and not the absence of filters, was the main reason the game looked flat.
    ///
    /// The map is keyed on the *Blender* material name, not the piece name, so a new kit piece
    /// that uses <c>M_Wall</c> is textured correctly the moment it is added, with no code change.
    ///
    /// Every replacement is triplanar: the kit has no UVs authored for tiling textures, and
    /// hand-unwrapping 31 pieces would have to be redone for each new one.
    /// </summary>
    public static class KitMaterials
    {
        /// <summary>Blender prefixes its materials; anything without this was assigned by the builder.</summary>
        private const string BlenderPrefix = "M_";

        private static readonly Dictionary<string, Material> Cache = new();

        public static void ResetCache() => Cache.Clear();

        /// <summary>Replaces every Blender-authored material on the instance and its children.</summary>
        public static void Remap(GameObject instance)
        {
            if (instance == null) return;

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !source.name.StartsWith(BlenderPrefix)) continue;

                    // A piece the artist has textured in Blender keeps its own material.
                    //
                    // This is the opt-out that makes hand-authored art possible: unwrap a piece,
                    // assign an image to its material, save, and Unity reimports it — the level
                    // then leaves it alone instead of overwriting the work with a procedural
                    // placeholder. Untextured pieces keep getting the level's materials, so the
                    // two ways of working coexist piece by piece.
                    if (IsHandTextured(source)) continue;

                    Material replacement = Resolve(StripDuplicateSuffix(source.name[BlenderPrefix.Length..]));
                    if (replacement == null || replacement == source) continue;

                    materials[i] = replacement;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// True when the material arrived from Blender with an image already assigned. Checks
        /// both property names because a .blend imported as URP uses <c>_BaseMap</c> while the
        /// built-in conversion path uses <c>_MainTex</c>.
        /// </summary>
        private static bool IsHandTextured(Material material)
        {
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null) return true;
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null) return true;
            return false;
        }

        /// <summary>
        /// Drops Blender's duplicate suffix. A piece whose material was copied inside the .blend
        /// arrives as <c>M_Wall.001</c>, which is the same material as far as the level cares —
        /// without this every copy falls through to the warning path.
        /// </summary>
        private static string StripDuplicateSuffix(string name)
        {
            int dot = name.LastIndexOf('.');
            if (dot <= 0 || name.Length - dot != 4) return name;

            for (int i = dot + 1; i < name.Length; i++)
                if (!char.IsDigit(name[i])) return name;

            return name[..dot];
        }

        /// <summary>
        /// The kit's 14 Blender materials, mapped onto the level's palette. Anything not listed
        /// falls through to the wall material rather than being left untextured, so a new
        /// Blender material shows up as obviously wrong instead of invisibly flat.
        /// </summary>
        private static Material Resolve(string blenderName)
        {
            if (Cache.TryGetValue(blenderName, out Material cached)) return cached;

            Material result = blenderName switch
            {
                "Wall" => Tri("KitWall", new Color(0.82f, 0.86f, 0.78f), 0.05f, LevelTextures.Wall),
                "Ceiling" => Tri("KitCeiling", new Color(0.78f, 0.82f, 0.74f), 0.03f, LevelTextures.Ceiling),
                "Floor" => Tri("KitFloor", new Color(0.78f, 0.82f, 0.76f), 0.15f, LevelTextures.FloorTile),

                // Concrete stairs: the wall texture reads as render at this scale, and a stair
                // is the one place a repeating tile pattern would look obviously wrong.
                "Stair" => Tri("KitStair", new Color(0.42f, 0.45f, 0.41f), 0.05f, LevelTextures.Wall, scale: 1.2f),

                "Wood" => Tri("KitWood", new Color(0.86f, 0.84f, 0.80f), 0.08f, LevelTextures.Wood, scale: 1.1f),
                "DoorWood" => Tri("KitDoorWood", new Color(0.66f, 0.60f, 0.56f), 0.10f, LevelTextures.Wood, scale: 1.4f),

                "Metal" => Tri("KitMetal", new Color(0.72f, 0.76f, 0.74f), 0.22f, LevelTextures.Metal, scale: 0.9f),
                "Fixture" => Tri("KitFixture", new Color(0.56f, 0.60f, 0.56f), 0.14f, LevelTextures.Metal, scale: 0.6f),
                "Generator" => Tri("KitGenerator", new Color(0.60f, 0.62f, 0.56f), 0.20f, LevelTextures.Metal, scale: 1.3f),

                "Board" => Tri("KitBoard", new Color(0.90f, 0.95f, 0.90f), 0.06f, LevelTextures.Board, scale: 2.4f),

                // Moulded plastic and PC cases: near-flat by nature, but a faint metal grain at a
                // small scale stops them reading as untextured colour under the torch.
                "Plastic" => Tri("KitPlastic", new Color(0.30f, 0.34f, 0.38f), 0.18f, LevelTextures.Metal, scale: 0.4f),
                "Case" => Tri("KitCase", new Color(0.34f, 0.35f, 0.36f), 0.16f, LevelTextures.Metal, scale: 0.5f),

                // A dead monitor is a black mirror. No texture, and the darkest thing in the room.
                "Screen" => Flat("KitScreen", new Color(0.045f, 0.05f, 0.055f), 0.55f),

                // Weak sheen, no emission: a dead tube must stay invisible until the torch finds
                // it. Emissive fittings are what made the first lighting pass glow in the dark.
                "Tube" => Flat("KitTube", new Color(0.40f, 0.42f, 0.38f), 0.20f),

                "DoorGlass" => Glass("KitDoorGlass", new Color(0.42f, 0.55f, 0.58f, 0.30f)),
                "Glass" => Glass("KitGlass", new Color(0.42f, 0.55f, 0.58f, 0.26f)),

                _ => Fallback(blenderName)
            };

            Cache[blenderName] = result;
            return result;
        }

        private static Material Fallback(string blenderName)
        {
            Debug.LogWarning($"[Kit] Blender material 'M_{blenderName}' has no mapping in KitMaterials; " +
                             "using the wall material. Add it to Resolve().");
            return Tri("KitWall", new Color(0.82f, 0.86f, 0.78f), 0.05f, LevelTextures.Wall);
        }

        private static Material Tri(string name, Color colour, float smoothness, Texture2D map,
                                    float scale = LevelTextures.MetresPerRepeat) =>
            LevelPrimitives.Mat(name, colour, smoothness, baseMap: map,
                                triplanar: true, triplanarScale: scale);

        private static Material Flat(string name, Color colour, float smoothness) =>
            LevelPrimitives.Mat(name, colour, smoothness);

        private static Material Glass(string name, Color colour) =>
            LevelPrimitives.Mat(name, colour, 0.85f, transparent: true);
    }
}
