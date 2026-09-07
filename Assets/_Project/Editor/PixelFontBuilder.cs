using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UtezHorror.EditorTools.Level;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Draws a 5x7 bitmap font and builds a Unity <see cref="Font"/> from it.
    ///
    /// The HUD has been using Unity's built-in vector font, which is rendered by an outline
    /// rasteriser designed for far more pixels than this game has. At 360p its glyphs land on
    /// fractional pixel boundaries, come out grey and uneven, and then get magnified by the
    /// nearest-neighbour upscale into a mess. Vector fonts do not survive this pipeline; bitmap
    /// fonts were what the era used, and they are readable at sizes where a vector font is soup.
    ///
    /// Each glyph is seven strings of five characters, which is a legible way to keep the shapes
    /// in source next to the code that packs them. Accented vowels and ñ are included because
    /// this game's text is Spanish and a missing glyph renders as a hollow box.
    /// </summary>
    public static class PixelFontBuilder
    {
        private const string TexturePath = "Assets/_Project/Art/Textures/UI/PixelFont.png";
        private const string FontPath = "Assets/_Project/Art/Fonts/PixelFont.fontsettings";

        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int Padding = 1;
        private const int Columns = 16;

        /// <summary>
        /// The glyphs, drawn. '#' is ink, anything else is transparent.
        ///
        /// Only what the HUD actually shows: digits, a colon, capitals, lowercase, and the
        /// Spanish accents. Adding one is seven lines here and nothing else.
        /// </summary>
        private static readonly Dictionary<char, string[]> Glyphs = new()
        {
            // The space. Easy to forget because it draws nothing, and its absence is spectacular:
            // without it Unity advances the pen by zero and every word in the game runs into the
            // next one. "HORROR UTEZ" came out as "HORRORUTEZ".
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },

            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            [':'] = new[] { ".....", "..#..", ".....", ".....", ".....", "..#..", "....." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
            [','] = new[] { ".....", ".....", ".....", ".....", ".##..", ".##..", ".#..." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['['] = new[] { "..###", "..#..", "..#..", "..#..", "..#..", "..#..", "..###" },
            [']'] = new[] { "###..", "..#..", "..#..", "..#..", "..#..", "..#..", "###.." },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "####.", "#...#", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "####.", "#....", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "####.", "#....", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#####", "#...#", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['Á'] = new[] { "...#.", ".###.", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['É'] = new[] { "...#.", "#####", "#....", "####.", "#....", "#....", "#####" },
            ['Í'] = new[] { "..#..", ".###.", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['Ó'] = new[] { "...#.", ".###.", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['Ú'] = new[] { "...#.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['Ñ'] = new[] { ".###.", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
            ['a'] = new[] { ".....", ".....", ".###.", "....#", ".####", "#...#", ".####" },
            ['b'] = new[] { "#....", "#....", "####.", "#...#", "#...#", "#...#", "####." },
            ['c'] = new[] { ".....", ".....", ".###.", "#....", "#....", "#....", ".###." },
            ['d'] = new[] { "....#", "....#", ".####", "#...#", "#...#", "#...#", ".####" },
            ['e'] = new[] { ".....", ".....", ".###.", "#...#", "#####", "#....", ".###." },
            ['f'] = new[] { "..##.", ".#...", "####.", ".#...", ".#...", ".#...", ".#..." },
            ['g'] = new[] { ".....", ".####", "#...#", "#...#", ".####", "....#", ".###." },
            ['h'] = new[] { "#....", "#....", "####.", "#...#", "#...#", "#...#", "#...#" },
            ['i'] = new[] { "..#..", ".....", ".##..", "..#..", "..#..", "..#..", ".###." },
            ['j'] = new[] { "...#.", ".....", "..##.", "...#.", "...#.", "#..#.", ".##.." },
            ['k'] = new[] { "#....", "#....", "#..#.", "#.#..", "##...", "#.#..", "#..#." },
            ['l'] = new[] { ".##..", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['m'] = new[] { ".....", ".....", "##.#.", "#.#.#", "#.#.#", "#...#", "#...#" },
            ['n'] = new[] { ".....", ".....", "####.", "#...#", "#...#", "#...#", "#...#" },
            ['o'] = new[] { ".....", ".....", ".###.", "#...#", "#...#", "#...#", ".###." },
            ['p'] = new[] { ".....", "####.", "#...#", "#...#", "####.", "#....", "#...." },
            ['q'] = new[] { ".....", ".####", "#...#", "#...#", ".####", "....#", "....#" },
            ['r'] = new[] { ".....", ".....", "#.##.", "##...", "#....", "#....", "#...." },
            ['s'] = new[] { ".....", ".....", ".####", "#....", ".###.", "....#", "####." },
            ['t'] = new[] { ".#...", ".#...", "####.", ".#...", ".#...", ".#..#", "..##." },
            ['u'] = new[] { ".....", ".....", "#...#", "#...#", "#...#", "#...#", ".####" },
            ['v'] = new[] { ".....", ".....", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['w'] = new[] { ".....", ".....", "#...#", "#...#", "#.#.#", "#.#.#", ".#.#." },
            ['x'] = new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
            ['y'] = new[] { ".....", "#...#", "#...#", "#...#", ".####", "....#", ".###." },
            ['z'] = new[] { ".....", ".....", "#####", "...#.", "..#..", ".#...", "#####" },
            ['á'] = new[] { "..#..", ".....", ".###.", "....#", ".####", "#...#", ".####" },
            ['é'] = new[] { "..#..", ".....", ".###.", "#...#", "#####", "#....", ".###." },
            ['í'] = new[] { "..#..", ".....", ".##..", "..#..", "..#..", "..#..", ".###." },
            ['ó'] = new[] { "..#..", ".....", ".###.", "#...#", "#...#", "#...#", ".###." },
            ['ú'] = new[] { "..#..", ".....", "#...#", "#...#", "#...#", "#...#", ".####" },
            ['ñ'] = new[] { ".###.", ".....", "####.", "#...#", "#...#", "#...#", "#...#" },
        };

        [MenuItem("Tools/UtezHorror/Build Pixel Font")]
        public static void Build()
        {
            var characters = new List<char>(Glyphs.Keys);
            characters.Sort();

            int rows = Mathf.CeilToInt(characters.Count / (float)Columns);
            int cellWidth = GlyphWidth + Padding * 2;
            int cellHeight = GlyphHeight + Padding * 2;
            int width = Mathf.NextPowerOfTwo(Columns * cellWidth);
            int height = Mathf.NextPowerOfTwo(rows * cellHeight);

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

            var infos = new List<CharacterInfo>(characters.Count);

            for (int index = 0; index < characters.Count; index++)
            {
                char c = characters[index];
                int col = index % Columns;
                int row = index / Columns;

                int originX = col * cellWidth + Padding;
                // Texture space runs bottom-up while the glyph strings read top-down.
                int originY = height - (row * cellHeight + Padding) - GlyphHeight;

                string[] shape = Glyphs[c];
                for (int y = 0; y < GlyphHeight; y++)
                for (int x = 0; x < GlyphWidth; x++)
                {
                    if (shape[y][x] != '#') continue;
                    int px = originX + x;
                    int py = originY + (GlyphHeight - 1 - y);
                    pixels[py * width + px] = new Color32(255, 255, 255, 255);
                }

                infos.Add(new CharacterInfo
                {
                    index = c,
                    advance = GlyphWidth + 1,
                    glyphWidth = GlyphWidth,
                    glyphHeight = GlyphHeight,
                    // Sits on the baseline with the descender allowance the glyphs are drawn for.
                    bearing = 0,
                    minX = 0,
                    maxX = GlyphWidth,
                    minY = -1,
                    maxY = GlyphHeight - 1,
                    uvBottomLeft = new Vector2(originX / (float)width, originY / (float)height),
                    uvBottomRight = new Vector2((originX + GlyphWidth) / (float)width, originY / (float)height),
                    uvTopLeft = new Vector2(originX / (float)width, (originY + GlyphHeight) / (float)height),
                    uvTopRight = new Vector2((originX + GlyphWidth) / (float)width,
                                             (originY + GlyphHeight) / (float)height)
                });
            }

            Texture2D texture = WriteTexture(pixels, width, height);
            Font font = WriteFont(texture, infos);

            Debug.Log($"[Font] Built {characters.Count} glyphs into {width}x{height}. " +
                      $"Use it at multiples of {GlyphHeight} px so it never resamples.");
            Selection.activeObject = font;
        }

        private static Texture2D WriteTexture(Color32[] pixels, int width, int height)
        {
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Textures/UI");

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.GUI;
            // Point and no mipmaps, or the whole reason for a bitmap font is undone by the
            // importer softening it back into grey.
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Font WriteFont(Texture2D texture, List<CharacterInfo> infos)
        {
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Fonts");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                font = new Font("PixelFont");
                AssetDatabase.CreateAsset(font, FontPath);
            }

            var material = new Material(Shader.Find("UI/Default")) { mainTexture = texture };
            material.name = "PixelFont";

            font.material = material;
            font.characterInfo = infos.ToArray();

            // These have to go through the serialised object: Font exposes them read-only.
            //
            // The ascent is the one that is easy to miss and impossible to ignore. Without it
            // Unity has no idea how tall a line is and stacks every line of a multi-line string
            // on the same baseline — the menu's two-line tagline rendered as one unreadable
            // smear of overlapping glyphs.
            var so = new SerializedObject(font);
            Set(so, "m_LineSpacing", GlyphHeight + 2);
            Set(so, "m_FontSize", GlyphHeight);
            Set(so, "m_Ascent", GlyphHeight);
            so.ApplyModifiedPropertiesWithoutUndo();

            // The material has to live inside the font asset or it is lost on reload.
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(FontPath))
                if (sub is Material old) Object.DestroyImmediate(old, allowDestroyingAssets: true);
            AssetDatabase.AddObjectToAsset(material, font);

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        }

        private static void Set(SerializedObject so, string property, float value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null) prop.floatValue = value;
        }

        /// <summary>The font asset, built on demand so the HUD builder can just ask for it.</summary>
        public static Font Load()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Build();
                font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            }
            return font;
        }
    }
}
