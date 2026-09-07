using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtezHorror.Environment;
using UtezHorror.UI;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Builds the title scene, generated like every other scene in this project.
    ///
    /// It is a camera looking at fog, and three lines of text. Nothing is loaded from the level:
    /// a menu that stands in the real building would have to keep the whole thing in memory to
    /// show a backdrop, and on a phone that is the difference between starting in two seconds and
    /// starting in twelve.
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Menu.unity";

        [MenuItem("Tools/UtezHorror/Build Menu Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The same atmosphere the run opens in, so the title and the first moment of play
            // share a colour and the cut between them is not a jolt.
            AtmospherePresets.Apply(AtmospherePresets.ExteriorLate);
            RenderSettings.skybox = null;

            var cameraGo = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            var camera = cameraGo.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = AtmospherePresets.ExteriorLate.fogColor;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            cameraGo.transform.position = new Vector3(0f, 1.6f, 0f);

            var root = new GameObject("Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.2f;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 1f;

            Text title = Label(root.transform, "Title", "HORROR UTEZ", 28, new Vector2(0f, 40f));
            title.color = new Color(0.86f, 0.84f, 0.76f, 0.95f);

            Text tagline = Label(root.transform, "Tagline",
                                 "Un turno. Cuatro componentes.\nNo enciendas la linterna más de lo necesario.",
                                 12, new Vector2(0f, 0f));
            tagline.color = new Color(0.66f, 0.68f, 0.62f, 0.9f);

            Text hint = Label(root.transform, "Hint", string.Empty, 14, new Vector2(0f, -50f));
            hint.color = new Color(0.74f, 0.72f, 0.66f, 0.9f);

            var menu = root.AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("hint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterFirst();

            Debug.Log($"[Menu] Built {ScenePath} and put it first in the build.");
        }

        /// <summary>
        /// Renders the title screen through its own camera.
        ///
        /// The only way to see the HUD font from batch mode: the run's HUD hangs off the player
        /// camera, which the level screenshot tool does not use, so the menu is the one screen a
        /// headless capture can actually show.
        /// </summary>
        [MenuItem("Tools/UtezHorror/Capture Menu Screenshot")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("[Menu] No camera in the menu scene.");
                return;
            }

            const int width = 640, height = 360;
            var rt = new RenderTexture(width, height, 24) { filterMode = FilterMode.Point };
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            System.IO.Directory.CreateDirectory("Screenshots");
            System.IO.File.WriteAllBytes("Screenshots/menu.png", texture.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log("[Menu] Wrote Screenshots/menu.png");
        }

        /// <summary>
        /// Puts the menu at index 0. The scene at index 0 is what a built player opens with, so
        /// forgetting this ships a game that drops straight into a dark corridor with no context.
        /// </summary>
        private static void RegisterFirst()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Text Label(Transform parent, string name, string content, int size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(560f, size * 3f);

            Text text = go.GetComponent<Text>();
            text.font = PixelFontBuilder.Load();
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}
