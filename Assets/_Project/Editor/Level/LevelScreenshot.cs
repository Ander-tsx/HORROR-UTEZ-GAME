using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UtezHorror.Environment;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Renders reference shots of the built level so the layout and the lighting can be
    /// checked without opening the editor. Used to verify the fixtures actually sit inside
    /// the building after the previous build put them all on the lawn.
    /// </summary>
    public static class LevelScreenshot
    {
        private const string OutputFolder = "Screenshots";

        [MenuItem("Tools/UtezHorror/Capture Level Screenshots")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(CecadecLevelBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(OutputFolder);

            // Cache references: GameObject.Find cannot see a deactivated object, so looking one
            // up again to switch it back on silently leaves it off.
            AtmospherePresets.Apply(AtmospherePresets.InteriorEarly);

            GameObject floor1Ceiling = Find("Floor1/Ceiling");
            GameObject floor2 = Find("Floor2");
            GameObject floor2Ceiling = Find("Floor2/Ceiling");
            GameObject parapet = Find("StairParapet");

            Toggle(false, floor1Ceiling, floor2, parapet);
            PlanShot("plan_floor1");
            Toggle(true, floor1Ceiling, floor2, parapet);

            Toggle(false, floor2Ceiling);
            PlanShot("plan_floor2", height: CecadecLayout.FloorToFloor);
            Toggle(true, floor2Ceiling);

            float up = CecadecLayout.FloorToFloor;

            Shot("eye_spawn", new Vector3(17f, 1.62f, 68.5f), new Vector3(4f, 180f, 0f));
            Shot("eye_corridor_north", new Vector3(17f, 1.62f, 56f), new Vector3(2f, 180f, 0f));
            Shot("eye_cross_corridor", new Vector3(30f, 1.62f, 18.9f), new Vector3(2f, 270f, 0f));
            Shot("eye_centro_computo", new Vector3(28f, 1.62f, 28f), new Vector3(6f, 0f, 0f));
            Shot("eye_stairwell_bottom", new Vector3(3.5f, 1.62f, 13.5f), new Vector3(4f, 0f, 0f));
            Shot("eye_stairwell_inside", new Vector3(3.5f, 1.62f, 16.0f), new Vector3(-14f, 25f, 0f));
            Shot("eye_stairwell_top", new Vector3(3.5f, 1.62f + up, 15.0f), new Vector3(6f, 0f, 0f));
            Shot("eye_corridor_floor2", new Vector3(17f, 1.62f + up, 56f), new Vector3(2f, 180f, 0f));
            Shot("ext_north", new Vector3(17f, 6f, 82f), new Vector3(14f, 180f, 0f));

            // The torch is the real light source now, so the level has to be checked with it on.
            Shot("torch_corridor_north", new Vector3(17f, 1.62f, 56f), new Vector3(2f, 180f, 0f), torch: true);
            Shot("torch_centro_computo", new Vector3(28f, 1.62f, 28f), new Vector3(6f, 0f, 0f), torch: true);
            Shot("torch_stairwell", new Vector3(3.5f, 1.62f, 15.2f), new Vector3(-6f, 20f, 0f), torch: true);
            Shot("torch_corridor_floor2", new Vector3(17f, 1.62f + up, 56f), new Vector3(2f, 180f, 0f), torch: true);

            // The professor at his first patrol stop, seen from down the corridor. The whole
            // point of the placeholder body is that a human outline reads before any detail.
            Shot("torch_enemy", new Vector3(17f, 1.62f, 48f), new Vector3(2f, 0f, 0f), torch: true);
            Shot("torch_enemy_close", new Vector3(17f, 1.62f, 51f), new Vector3(0f, 0f, 0f), torch: true);
            Shot("torch_bench", new Vector3(15.5f, 1.62f, 38f), new Vector3(8f, 250f, 0f), torch: true);
            Shot("torch_wall_closeup", new Vector3(15.5f, 1.55f, 56f), new Vector3(0f, 270f, 0f), torch: true);
            Shot("torch_floor_closeup", new Vector3(17f, 1.62f, 56f), new Vector3(52f, 180f, 0f), torch: true);

            // Atmosphere is a runtime system, so a capture has to pick the preset itself or
            // every outdoor shot comes out lit like a corridor.
            AtmospherePresets.Apply(AtmospherePresets.ExteriorEarly);

            // The outdoor world: the crossing is the part that has to sell the fog.
            Shot("out_path_to_cds", new Vector3(17f, 1.62f, 68f), new Vector3(2f, 348f, 0f), torch: true);
            Shot("out_wood", new Vector3(0f, 1.62f, 88f), new Vector3(2f, 20f, 0f), torch: true);
            Shot("out_cds_entrance", new Vector3(4f, 1.62f, 96f), new Vector3(2f, 0f, 0f), torch: true);
            Shot("out_cecadec_facade", new Vector3(17f, 2.2f, 78f), new Vector3(4f, 180f, 0f), torch: true);

            AtmospherePresets.Apply(AtmospherePresets.InteriorEarly);

            // Art reference, NOT what the game looks like: an even fill light so the surface
            // work can be judged on its own. In the dark every texture reads as the same black,
            // so lighting problems and texture problems are impossible to tell apart otherwise.
            ArtShot("art_wall", new Vector3(15.5f, 1.55f, 56f), new Vector3(0f, 270f, 0f));
            ArtShot("art_floor", new Vector3(17f, 1.62f, 56f), new Vector3(52f, 180f, 0f));
            ArtShot("art_corridor", new Vector3(17f, 1.62f, 56f), new Vector3(2f, 180f, 0f));

            Debug.Log($"[Screenshot] Wrote captures to {Path.GetFullPath(OutputFolder)}");
        }

        /// <summary>
        /// Same framing as a gameplay shot but lit flat and wide, purely so the texture and
        /// palette work is visible. Never use one of these to judge how the game reads.
        /// </summary>
        private static void ArtShot(string name, Vector3 position, Vector3 euler)
        {
            var fill = new GameObject("ArtFill");
            fill.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            var light = fill.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 22f;
            light.intensity = 7f;
            light.color = Color.white;
            light.shadows = LightShadows.None;

            Shot(name, position, euler);

            Object.DestroyImmediate(fill);
        }

        /// <summary>Looks up a descendant of the level root by path, active or not.</summary>
        private static GameObject Find(string path)
        {
            var root = GameObject.Find("Cecadec");
            Transform child = root != null ? root.transform.Find(path) : null;
            if (child == null) Debug.LogWarning($"[Screenshot] 'Cecadec/{path}' not found.");
            return child != null ? child.gameObject : null;
        }

        private static void Toggle(bool active, params GameObject[] targets)
        {
            foreach (GameObject go in targets)
                if (go != null) go.SetActive(active);
        }

        /// <summary>
        /// Top-down blueprint view. Adds a temporary bright light because the level is lit for
        /// horror, which makes an unlit plan render unreadable.
        /// </summary>
        private static void PlanShot(string name, float centreZ = 30.35f, float height = 0f)
        {
            var sun = new GameObject("PlanLight");
            sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = Color.white;
            light.shadows = LightShadows.None;

            Shot(name, new Vector3(17f, 70f + height, centreZ), new Vector3(90f, 0f, 0f),
                 orthographic: true, orthoSize: 34f, width: 1100, height: 1500, pixelate: false);

            Object.DestroyImmediate(sun);
        }

        /// <summary>
        /// Lines the low-resolution render is taken at, matching the shipped render scale.
        /// URP **ignores render scale for a camera drawing into a RenderTexture**, so a capture
        /// that just renders at full size shows a game that does not exist: smooth edges the
        /// player will never see. The shot is rendered small and blown back up point-sampled,
        /// which is what the pipeline does on screen.
        /// </summary>
        private const int RenderLines = 360;

        private static void Shot(string name, Vector3 position, Vector3 euler,
                                 bool orthographic = false, float orthoSize = 10f,
                                 int width = 1280, int height = 720, bool torch = false,
                                 bool pixelate = true)
        {
            var go = new GameObject("CaptureCamera");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));

            var cam = go.AddComponent<Camera>();
            go.AddComponent<UniversalAdditionalCameraData>();
            cam.orthographic = orthographic;
            cam.orthographicSize = orthoSize;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Matches what AtmosphereController does at runtime: the fog is the sky, so a
            // capture with a black clear colour shows a horizon the player never sees.
            cam.backgroundColor = RenderSettings.fogColor;

            // Mirrors the PhoneTorch built by PlayerRigBuilder.
            if (torch)
            {
                var beamGo = new GameObject("CaptureTorch");
                beamGo.transform.SetParent(go.transform, false);
                var beam = beamGo.AddComponent<Light>();
                beam.type = LightType.Spot;
                beam.range = 26f;
                beam.spotAngle = 40f;
                beam.innerSpotAngle = 14f;
                beam.intensity = 34f;
                beam.color = new Color(0.92f, 0.94f, 1f);
                beam.shadows = LightShadows.Hard;
            }

            int lowHeight = pixelate ? RenderLines : height;
            int lowWidth = pixelate ? Mathf.RoundToInt(width * (RenderLines / (float)height)) : width;

            // No MSAA either: anti-aliasing smooths the exact staircase the style is built on.
            var rt = new RenderTexture(lowWidth, lowHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Point
            };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(lowWidth, lowHeight, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point
            };
            tex.ReadPixels(new Rect(0, 0, lowWidth, lowHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            Texture2D output = pixelate ? Upscale(tex, width, height) : tex;
            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), output.EncodeToPNG());
            if (output != tex) Object.DestroyImmediate(output);

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Nearest-neighbour blow-up, so a PNG opened at 100% shows the same hard pixel edges
        /// the upscaling filter produces on screen. Bilinear here would hide the whole effect.
        /// </summary>
        private static Texture2D Upscale(Texture2D source, int width, int height)
        {
            var scaled = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color32[] src = source.GetPixels32();
            var dst = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sy = Mathf.Min(source.height - 1, y * source.height / height);
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.Min(source.width - 1, x * source.width / width);
                    dst[y * width + x] = src[sy * source.width + sx];
                }
            }

            scaled.SetPixels32(dst);
            scaled.Apply();
            return scaled;
        }
    }
}
