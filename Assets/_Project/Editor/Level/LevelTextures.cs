using System.IO;
using UnityEditor;
using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Tiling textures for the level, authored to PS1-era rules.
    ///
    /// Three deliberate constraints, all of them things that look like mistakes if you do not
    /// know the art direction (see Docs/Plans/02-estilo-visual-ps1.md):
    ///
    /// 1. **64 px/m.** A PS1 sat between 16 and 64 px/m. The previous pass was authored at
    ///    256 px/m, which is roughly four times sharper than anything the console could hold
    ///    and reads as a modern game with drab textures rather than a period one.
    /// 2. **Point filtering, no mipmaps.** The console had neither. The aliasing that shows up
    ///    at distance is part of the look, not a bug — but it is also the thing most likely to
    ///    read as noise on a phone, so it needs checking in the Mobile tier before it is
    ///    called done. (Foliage is an explicit exception and *does* get mipmaps.)
    /// 3. **Quantised palette.** PS1 textures were 4- or 8-bit palettised, so values step
    ///    instead of gliding. <see cref="Quantise"/> reproduces that; without it a procedural
    ///    Perlin texture reads as a smooth modern gradient no matter how low its resolution is.
    ///
    /// Palette reference is Silent Hill 1: dirty greys, sick green, rust and water staining
    /// over institutional surfaces. Nothing here is allowed to be clean white.
    /// </summary>
    public static class LevelTextures
    {
        public const int Resolution = 128;
        public const float MetresPerRepeat = 2f;
        public const float TexelsPerMetre = Resolution / MetresPerRepeat;   // 64

        /// <summary>
        /// Value steps per channel. Low enough to see the stepping, high enough to read as a
        /// surface. Note the knock-on: each step is ~7% of the range, so any variation weaker
        /// than that rounds away and the surface comes out a flat colour. The noise amplitudes
        /// below are sized against this number, not picked by eye.
        /// </summary>
        private const int PaletteLevels = 14;

        private const string Folder = "Assets/_Project/Art/Textures/Level";

        public static Texture2D FloorTile => Get("FloorTile", BuildFloorTile);
        public static Texture2D Wall => Get("Wall", BuildWall);
        public static Texture2D Ceiling => Get("Ceiling", BuildCeiling);
        public static Texture2D Wood => Get("Wood", BuildWood);
        public static Texture2D Metal => Get("Metal", BuildMetal);
        public static Texture2D Board => Get("Board", BuildBoard);

        public static Texture2D Ground => Get("Ground", BuildGround);
        public static Texture2D Bark => Get("Bark", BuildBark);

        /// <summary>
        /// Tree canopy, cut out with alpha. The one texture in the project that keeps its
        /// mipmaps — see <see cref="Get"/>.
        /// </summary>
        public static Texture2D Canopy => Get("Canopy", BuildCanopy, alphaCut: true);

        /// <summary>
        /// Blood splatter for the death overlay. A UI texture rather than a surface one, but it
        /// lives here so every generated image in the project is made the same way and can be
        /// rebuilt from one menu item.
        /// </summary>
        public static Texture2D Blood => Get("Blood", BuildBlood, alphaCut: true);

        private static readonly string[] All =
        {
            "FloorTile", "Wall", "Ceiling", "Wood", "Metal", "Board", "Ground", "Bark", "Canopy",
            "Blood"
        };

        [MenuItem("Tools/UtezHorror/Regenerate Placeholder Textures")]
        public static void RegenerateAll()
        {
            foreach (string name in All)
                AssetDatabase.DeleteAsset($"{Folder}/Tex_{name}.png");

            _ = FloorTile;
            _ = Wall;
            _ = Ceiling;
            _ = Wood;
            _ = Metal;
            _ = Board;
            _ = Ground;
            _ = Bark;
            _ = Canopy;
            _ = Blood;
            Debug.Log($"[Textures] Regenerated at {TexelsPerMetre:0} px/m, " +
                      $"{Resolution}px, {PaletteLevels} levels, point-filtered, no mipmaps.");
        }

        private static Texture2D Get(string name, System.Func<Texture2D> build, bool alphaCut = false)
        {
            string path = $"{Folder}/Tex_{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            LevelPrimitives.EnsureFolder(Folder);
            Texture2D generated = build();
            File.WriteAllBytes(path, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;   // without this, tiling shows seams
            importer.filterMode = FilterMode.Point;       // the PS1 had no bilinear filter

            // Alpha-cut textures are the deliberate exception to the no-mipmaps rule. Cut-out
            // leaves or perforations at 360p with no mip chain boil into a field of crawling
            // pixels the moment the camera moves — which reads as a broken effect, not as a
            // period one.
            importer.mipmapEnabled = alphaCut;
            importer.alphaIsTransparency = alphaCut;
            importer.sRGBTexture = true;
            importer.maxTextureSize = Resolution;
            // Block compression on a 128px texture would spend its error budget smearing the
            // hard palette steps this is built on, and saves a rounding error's worth of memory.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Blank() =>
            new(Resolution, Resolution, TextureFormat.RGB24, mipChain: false);

        private static Texture2D BlankAlpha() =>
            new(Resolution, Resolution, TextureFormat.RGBA32, mipChain: false);

        /// <summary>
        /// Splatter, densest at the edges of the frame.
        ///
        /// Weighted to the border on purpose: blood over the centre of the screen hides the thing
        /// that just killed you, which is the one image the moment needs to land. It frames the
        /// view rather than covering it.
        /// </summary>
        private static Texture2D BuildBlood()
        {
            Texture2D tex = BlankAlpha();
            var pixels = new Color32[Resolution * Resolution];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

            const int blobs = 40;
            for (int b = 0; b < blobs; b++)
            {
                // Pushed outwards from the middle: 1 - hash^2 clusters values near the edges.
                float angle = Hash(b, 3) * Mathf.PI * 2f;
                float edge = 0.45f + Hash(b, 17) * 0.55f;
                float cx = Resolution * (0.5f + Mathf.Cos(angle) * edge * 0.5f);
                float cy = Resolution * (0.5f + Mathf.Sin(angle) * edge * 0.5f);
                float radius = Resolution * (0.03f + Hash(b, 41) * 0.09f);

                int r = Mathf.CeilToInt(radius);
                for (int y = Mathf.Max(0, (int)cy - r); y < Mathf.Min(Resolution, cy + r); y++)
                for (int x = Mathf.Max(0, (int)cx - r); x < Mathf.Min(Resolution, cx + r); x++)
                {
                    float dx = (x - cx) / radius;
                    float dy = (y - cy) / radius;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;

                    float ragged = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                    if (d > 0.4f && ragged < d * 0.9f) continue;

                    float value = 0.30f + (1f - d) * 0.25f;
                    byte alpha = (byte)(Mathf.Clamp01(1.15f - d) * 255f);
                    Color32 blood = Tinted(value, new Vector3(1.0f, 0.16f, 0.12f));
                    pixels[y * Resolution + x] = new Color32(blood.r, blood.g, blood.b, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Bare earth with dead grass: the ground between the two buildings.</summary>
        private static Texture2D BuildGround()
        {
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float clods = (Mathf.PerlinNoise(x * 0.30f, y * 0.30f) - 0.5f) * 0.24f;
                float patch = (Mathf.PerlinNoise(x * 0.035f, y * 0.035f) - 0.5f) * 0.20f;
                // Sparse blades, so the ground is not a uniform brown sheet under the torch.
                float blade = Mathf.Max(0f, Mathf.PerlinNoise(x * 1.9f, y * 0.35f) - 0.66f) * 0.9f;

                float value = 0.34f + clods + patch + blade * 0.5f;
                // Dead grass is yellow-green; wet earth under it is grey-brown.
                Vector3 hue = blade > 0.05f ? new Vector3(0.92f, 1.0f, 0.55f)
                                            : new Vector3(1.0f, 0.90f, 0.70f);
                pixels[y * Resolution + x] = Tinted(value, hue);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Bark: vertical fissures, dark and desaturated.</summary>
        private static Texture2D BuildBark()
        {
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                // Stretched hard on Y so the grain runs up the trunk.
                float fissure = Mathf.PerlinNoise(x * 0.55f, y * 0.05f);
                float value = 0.20f + (fissure - 0.5f) * 0.30f;
                pixels[y * Resolution + x] = Tinted(value, new Vector3(1.0f, 0.88f, 0.70f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// A cut-out clump of leaves on a transparent background, for the crossed quads that make
        /// up a tree. Built as blobs rather than noise: a noise-based alpha cuts into confetti at
        /// this resolution, while blobs read as foliage even when only a few pixels across.
        /// </summary>
        private static Texture2D BuildCanopy()
        {
            Texture2D tex = BlankAlpha();
            var pixels = new Color32[Resolution * Resolution];
            const int clumps = 26;

            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

            for (int c = 0; c < clumps; c++)
            {
                // Weighted towards the middle, so the silhouette is a canopy and not a square.
                float cx = Resolution * (0.5f + (Hash(c, 11) - 0.5f) * 0.78f);
                float cy = Resolution * (0.5f + (Hash(c, 29) - 0.5f) * 0.78f);
                float radius = Resolution * (0.09f + Hash(c, 47) * 0.10f);
                float shade = 0.14f + Hash(c, 71) * 0.20f;

                int r = Mathf.CeilToInt(radius);
                for (int y = Mathf.Max(0, (int)cy - r); y < Mathf.Min(Resolution, cy + r); y++)
                for (int x = Mathf.Max(0, (int)cx - r); x < Mathf.Min(Resolution, cx + r); x++)
                {
                    float dx = (x - cx) / radius;
                    float dy = (y - cy) / radius;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;

                    // Ragged edge: a clean circle reads as a ball, not as leaves.
                    float ragged = Mathf.PerlinNoise(x * 0.55f, y * 0.55f);
                    if (d > 0.55f && ragged < d) continue;

                    float value = shade * (1.15f - d * 0.4f)
                                + (Mathf.PerlinNoise(x * 0.9f, y * 0.9f) - 0.5f) * 0.07f;

                    Color32 leaf = Tinted(value, new Vector3(0.78f, 1.0f, 0.62f));
                    pixels[y * Resolution + x] = new Color32(leaf.r, leaf.g, leaf.b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Institutional floor tile: 3 tiles across the 2 m repeat, so 66 cm tiles.</summary>
        private static Texture2D BuildFloorTile()
        {
            const int tiles = 3;
            const float grout = 0.06f;
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float u = x / (float)Resolution * tiles;
                float v = y / (float)Resolution * tiles;
                float fu = u - Mathf.Floor(u);
                float fv = v - Mathf.Floor(v);

                bool isGrout = fu < grout || fu > 1f - grout || fv < grout || fv > 1f - grout;

                // Per-tile value variation so a floor never reads as one flat sheet.
                float tileShade = Hash((int)u, (int)v) * 0.26f - 0.13f;
                float grain = (Mathf.PerlinNoise(x * 0.22f, y * 0.22f) - 0.5f) * 0.16f;

                float value = isGrout ? 0.16f : 0.42f + tileShade + grain;

                // Standing water and old mopping: broad dark patches, not uniform dirt.
                float damp = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.018f, y * 0.018f) - 0.48f) * 0.9f;
                value -= damp;

                pixels[y * Resolution + x] = Sick(value, rust: Rust(x, y, 0.55f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Painted wall: mostly flat, with mottling and grime creeping up from the skirting.</summary>
        private static Texture2D BuildWall()
        {
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float mottle = (Mathf.PerlinNoise(x * 0.05f, y * 0.05f) - 0.5f) * 0.26f;
                float grain = (Mathf.PerlinNoise(x * 0.6f, y * 0.6f) - 0.5f) * 0.11f;

                // Vertical streaking, strongest near the bottom of the repeat.
                float height01 = y / (float)Resolution;
                float streak = (Mathf.PerlinNoise(x * 0.2f, y * 0.024f) - 0.5f)
                               * 0.30f * Mathf.Clamp01(1f - height01 * 1.6f);

                // Damp bleeding down from above: the single most useful stain on an
                // institutional wall, because it tells you the building leaks.
                float bleed = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.09f, y * 0.012f) - 0.55f)
                              * 0.8f * Mathf.Clamp01(height01 * 1.4f);

                float value = 0.46f + mottle + grain - Mathf.Abs(streak) - bleed;
                pixels[y * Resolution + x] = Sick(value, rust: Rust(x, y, 0.62f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Suspended ceiling: 2 panels across the repeat, so 1 m panels.</summary>
        private static Texture2D BuildCeiling()
        {
            const int panels = 2;
            const float seam = 0.04f;
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float u = x / (float)Resolution * panels;
                float v = y / (float)Resolution * panels;
                float fu = u - Mathf.Floor(u);
                float fv = v - Mathf.Floor(v);

                bool isSeam = fu < seam || fu > 1f - seam || fv < seam || fv > 1f - seam;
                float speckle = (Mathf.PerlinNoise(x * 0.9f, y * 0.9f) - 0.5f) * 0.14f;

                // Ceiling panels stain from above, and it is the stains that sell "abandoned".
                float stain = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.03f, y * 0.03f) - 0.5f) * 1.1f;

                float value = (isSeam ? 0.18f : 0.50f + speckle) - stain;
                pixels[y * Resolution + x] = Sick(value, rust: Rust(x, y, 0.5f) + stain * 0.5f);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Furniture wood: planks along one axis, with the grain and the scuffing that stops a
        /// desk reading as a beige box. Half the surfaces the torch actually lands on are
        /// furniture, so this matters more than its size suggests.
        /// </summary>
        private static Texture2D BuildWood()
        {
            const int planks = 4;
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float v = y / (float)Resolution * planks;
                float fv = v - Mathf.Floor(v);
                bool isSeam = fv < 0.03f || fv > 0.97f;

                float plankShade = Hash(0, (int)v) * 0.22f - 0.11f;
                // Grain runs along the plank, so the noise is stretched on one axis.
                float grain = (Mathf.PerlinNoise(x * 0.07f, y * 1.4f) - 0.5f) * 0.20f;
                float wear = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.04f, y * 0.04f) - 0.55f) * 0.7f;

                float value = (isSeam ? 0.16f : 0.40f + plankShade + grain) - wear;
                pixels[y * Resolution + x] = Tinted(value, new Vector3(1.0f, 0.68f, 0.42f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Painted steel: mostly flat, scratched, rusting from the bottom edge.</summary>
        private static Texture2D BuildMetal()
        {
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float brushed = (Mathf.PerlinNoise(x * 1.6f, y * 0.05f) - 0.5f) * 0.13f;
                float dent = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.12f;

                float value = 0.38f + brushed + dent;
                float rust = Rust(x, y, 0.5f) * Mathf.Clamp01(1.4f - y / (float)Resolution * 1.4f);

                pixels[y * Resolution + x] = Sick(value, rust);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Classroom board: dark green, ghosted with chalk that never fully came off.</summary>
        private static Texture2D BuildBoard()
        {
            Texture2D tex = Blank();
            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                // Wide horizontal smears: the mark a board rubber leaves.
                float smear = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.05f, y * 0.5f) - 0.45f) * 0.55f;
                float grain = (Mathf.PerlinNoise(x * 0.8f, y * 0.8f) - 0.5f) * 0.05f;

                float value = 0.16f + smear + grain;
                pixels[y * Resolution + x] = Tinted(value, new Vector3(0.72f, 1.0f, 0.78f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Quantised value pushed through an arbitrary hue, for surfaces that are not grey.</summary>
        private static Color32 Tinted(float value, Vector3 hue)
        {
            value = Quantise(Mathf.Clamp01(value));
            return new Color32(
                (byte)(Quantise(Mathf.Clamp01(value * hue.x)) * 255f),
                (byte)(Quantise(Mathf.Clamp01(value * hue.y)) * 255f),
                (byte)(Quantise(Mathf.Clamp01(value * hue.z)) * 255f),
                255);
        }

        /// <summary>
        /// Silent Hill palette in one place: a green-grey base pushed towards rust wherever
        /// <paramref name="rust"/> says the surface has been wet. The green tint is what stops
        /// the building reading as neutral concrete, and it is subtle on purpose — a strong
        /// green reads as a colour filter rather than as a sick building.
        /// </summary>
        private static Color32 Sick(float value, float rust)
        {
            value = Quantise(Mathf.Clamp01(value));
            rust = Mathf.Clamp01(rust);

            // Green-grey: green channel highest, blue lowest.
            var baseColour = new Vector3(value * 0.94f, value, value * 0.86f);
            // Old iron staining: warm, and always darker than the surface it sits on.
            var rustColour = new Vector3(value * 0.86f, value * 0.55f, value * 0.34f);

            Vector3 c = Vector3.Lerp(baseColour, rustColour, rust * 0.85f);

            return new Color32(
                (byte)(Quantise(Mathf.Clamp01(c.x)) * 255f),
                (byte)(Quantise(Mathf.Clamp01(c.y)) * 255f),
                (byte)(Quantise(Mathf.Clamp01(c.z)) * 255f),
                255);
        }

        /// <summary>Snaps a 0..1 value to the palette's steps. This is what makes it read as 1997.</summary>
        private static float Quantise(float value) =>
            Mathf.Round(value * (PaletteLevels - 1)) / (PaletteLevels - 1);

        /// <summary>Patchy rust/damp mask. <paramref name="threshold"/> raises it to make rust rarer.</summary>
        private static float Rust(int x, int y, float threshold)
        {
            float n = Mathf.PerlinNoise(x * 0.035f + 11.3f, y * 0.035f + 7.9f);
            return Mathf.Max(0f, n - threshold) / (1f - threshold);
        }

        private static float Hash(int x, int y)
        {
            int n = x * 73856093 ^ y * 19349663;
            n = n ^ (n >> 13);
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}
