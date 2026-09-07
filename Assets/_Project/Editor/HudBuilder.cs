using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;
using UtezHorror.UI;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Assembles the run HUD: a battery meter and an interaction reticle.
    ///
    /// Built in code like every other part of this project's scenes, so a rebuild cannot lose it
    /// and there is one place to read what the HUD actually contains.
    ///
    /// The canvas renders in **Screen Space - Camera**, not Overlay. That is the decision from
    /// Docs/Plans/02: an Overlay canvas draws after everything at the window's native
    /// resolution, so the HUD would be crisp and modern while the world behind it is 360p — the
    /// two would obviously not belong to the same game. On the camera, the HUD goes through the
    /// same low-resolution render and the same nearest-neighbour upscale as the world.
    ///
    /// The direct consequence: everything here has to be legible at 360p. That is why the
    /// battery is blocks rather than a number, and why the reticle carries as much information
    /// as the text does.
    /// </summary>
    public static class HudBuilder
    {
        /// <summary>Blocks in the battery meter. Eight reads as a phone battery at a glance.</summary>
        private const int BatterySegments = 8;

        /// <summary>
        /// Reference height the layout is authored against, in the low-resolution buffer's own
        /// pixels. Matching the render resolution means one unit here is one rendered pixel, so
        /// a 4 px gap really is 4 px and not a fraction of one.
        /// </summary>
        private const float ReferenceHeight = 360f;

        public static GameObject Build(Camera camera)
        {
            var root = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.2f;   // inside the near plane's reach, ahead of nothing else

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceHeight * 16f / 9f, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // match height: the HUD hugs the bottom edge

            BuildBattery(root.transform);
            BuildVisibility(root.transform);
            BuildReticle(root.transform);
            BuildObjectives(root.transform);
            BuildDeathScreen(root.transform);
            BuildRunEnd(root.transform);
            BuildPause(root.transform);
            BuildTouch(root.transform);

            return root;
        }

        private static void BuildBattery(Transform parent)
        {
            var group = new GameObject("Battery", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);

            // Bottom-left, out of the way of anything the player is looking at.
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(10f, 10f);
            rect.sizeDelta = new Vector2(BatterySegments * 7f, 12f);

            var segments = new Image[BatterySegments];
            for (int i = 0; i < BatterySegments; i++)
            {
                var block = new GameObject($"Segment{i}", typeof(RectTransform), typeof(Image));
                var blockRect = (RectTransform)block.transform;
                blockRect.SetParent(rect, false);
                blockRect.anchorMin = blockRect.anchorMax = new Vector2(0f, 0.5f);
                blockRect.pivot = new Vector2(0f, 0.5f);
                blockRect.sizeDelta = new Vector2(5f, 10f);
                blockRect.anchoredPosition = new Vector2(i * 7f, 0f);

                Image image = block.GetComponent<Image>();
                image.raycastTarget = false;   // nothing in this HUD is clickable
                segments[i] = image;
            }

            var meter = group.AddComponent<BatteryMeter>();
            var so = new SerializedObject(meter);
            SerializedProperty list = so.FindProperty("segments");
            list.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Exposure, above the battery. Six blocks that fill and warm as you become easier to
        /// see — the only way a player can learn what crouching, sprinting and the torch
        /// actually cost them without dying repeatedly and guessing.
        /// </summary>
        private static void BuildVisibility(Transform parent)
        {
            const int count = 6;

            var group = new GameObject("Visibility", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(10f, 26f);   // directly above the battery row
            rect.sizeDelta = new Vector2(count * 7f, 8f);

            var segments = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var block = new GameObject($"Eye{i}", typeof(RectTransform), typeof(Image));
                var blockRect = (RectTransform)block.transform;
                blockRect.SetParent(rect, false);
                blockRect.anchorMin = blockRect.anchorMax = new Vector2(0f, 0.5f);
                blockRect.pivot = new Vector2(0f, 0.5f);
                blockRect.sizeDelta = new Vector2(5f, 6f);
                blockRect.anchoredPosition = new Vector2(i * 7f, 0f);

                Image image = block.GetComponent<Image>();
                image.raycastTarget = false;
                segments[i] = image;
            }

            var meter = group.AddComponent<VisibilityMeter>();
            var so = new SerializedObject(meter);
            SerializedProperty list = so.FindProperty("segments");
            list.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The shopping list top-right, the clock above it, and the theft bar under the reticle.
        ///
        /// The list is what tells the player the run has a point, so it is always on screen. The
        /// theft bar is not: it appears when you commit to taking something and vanishes when you
        /// let go, which is the difference between feedback and furniture.
        /// </summary>
        private static void BuildObjectives(Transform parent)
        {
            var group = new GameObject("Objectives", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            Text clock = Label(rect, "Clock", "00:00", 14, Vector2.zero);
            var clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.pivot = new Vector2(1f, 1f);
            clockRect.anchoredPosition = new Vector2(-10f, -8f);
            clockRect.sizeDelta = new Vector2(70f, 18f);
            clock.alignment = TextAnchor.UpperRight;

            Text list = Label(rect, "List", string.Empty, 7, Vector2.zero);
            var listRect = (RectTransform)list.transform;
            listRect.anchorMin = listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot = new Vector2(1f, 1f);
            listRect.anchoredPosition = new Vector2(-10f, -28f);
            listRect.sizeDelta = new Vector2(130f, 70f);
            list.alignment = TextAnchor.UpperRight;
            list.color = new Color(0.80f, 0.80f, 0.72f, 0.85f);

            // Under the reticle, where the player is already looking while working on a machine.
            var theft = new GameObject("Theft", typeof(RectTransform));
            var theftRect = (RectTransform)theft.transform;
            theftRect.SetParent(rect, false);
            theftRect.anchorMin = theftRect.anchorMax = new Vector2(0.5f, 0.5f);
            theftRect.pivot = new Vector2(0.5f, 0.5f);
            theftRect.anchoredPosition = new Vector2(0f, -32f);
            theftRect.sizeDelta = new Vector2(90f, 20f);

            var backGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            var backRect = (RectTransform)backGo.transform;
            backRect.SetParent(theftRect, false);
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.sizeDelta = new Vector2(88f, 5f);
            Image track = backGo.GetComponent<Image>();
            track.color = new Color(0.08f, 0.09f, 0.08f, 0.75f);
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(backRect, false);
            Stretch(fillRect);
            Image fill = fillGo.GetComponent<Image>();
            fill.raycastTarget = false;
            // Filled horizontally from the left, so the bar reads as progress rather than as a
            // meter that happens to be shrinking.
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            Text label = Label(theftRect, "Label", string.Empty, 7, new Vector2(0f, -12f));
            label.color = new Color(0.88f, 0.86f, 0.78f, 0.9f);

            var hud = group.AddComponent<ObjectiveHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("list").objectReferenceValue = list;
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("theftGroup").objectReferenceValue = theft;
            so.FindProperty("theftFill").objectReferenceValue = fill;
            so.FindProperty("theftLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            theft.SetActive(false);
        }

        /// <summary>
        /// The blood overlay only. The loss panel lives in RunEndScreen, because the clock
        /// running out ends a run too and both endings have to reach the same screen.
        /// </summary>
        private static void BuildDeathScreen(Transform parent)
        {
            var group = new GameObject("Death", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            var bloodGo = new GameObject("Blood", typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)bloodGo.transform);
            bloodGo.transform.SetParent(rect, false);
            Stretch((RectTransform)bloodGo.transform);
            Image blood = bloodGo.GetComponent<Image>();
            blood.sprite = Sprite.Create(Level.LevelTextures.Blood,
                                         new Rect(0, 0, Level.LevelTextures.Resolution,
                                                  Level.LevelTextures.Resolution),
                                         new Vector2(0.5f, 0.5f));
            blood.raycastTarget = false;
            blood.enabled = false;

            var screen = group.AddComponent<DeathScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("blood").objectReferenceValue = blood;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The screen that closes a run, with the summary on it. Separate from the blood overlay
        /// on purpose: the blood lands on the impact, this arrives seconds later.
        /// </summary>
        private static void BuildRunEnd(Transform parent)
        {
            var group = new GameObject("RunEnd", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(rect, false);
            Stretch(panelRect);
            Image backdrop = panel.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.80f);
            backdrop.raycastTarget = false;

            Text headline = Label(panelRect, "Headline", "FIN DEL TURNO", 21, new Vector2(0f, 34f));
            Text summary = Label(panelRect, "Summary", string.Empty, 14, new Vector2(0f, 2f));
            Text hint = Label(panelRect, "Hint", string.Empty, 7, new Vector2(0f, -34f));
            hint.color = new Color(0.72f, 0.70f, 0.64f, 0.9f);

            panel.SetActive(false);

            var screen = group.AddComponent<RunEndScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("headline").objectReferenceValue = headline;
            so.FindProperty("summary").objectReferenceValue = summary;
            so.FindProperty("hint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPause(Transform parent)
        {
            var group = new GameObject("Pause", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(rect, false);
            Stretch(panelRect);
            Image backdrop = panel.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.72f);
            backdrop.raycastTarget = false;

            Label(panelRect, "Title", "PAUSA", 21, new Vector2(0f, 40f));
            Text body = Label(panelRect, "Body", string.Empty, 14, new Vector2(0f, 0f));
            body.color = new Color(0.80f, 0.78f, 0.72f, 0.92f);

            panel.SetActive(false);

            var menu = group.AddComponent<PauseMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The on-screen stick and buttons, hidden unless the player is actually touching a
        /// screen. They drive the same action asset as the keyboard, so nothing downstream of
        /// PlayerInputRouter learns that a phone exists.
        ///
        /// Placed with generous margins: a control under a thumb is a control the player cannot
        /// see past, and this game is played by looking into dark corners.
        /// </summary>
        private static void BuildTouch(Transform parent)
        {
            var group = new GameObject("Touch", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            Stick(rect, "MoveStick", new Vector2(0f, 0f), new Vector2(64f, 56f), "<Gamepad>/leftStick");

            Button(rect, "Interact", new Vector2(1f, 0f), new Vector2(-40f, 100f), 34f, "<Keyboard>/e");
            Button(rect, "Sprint", new Vector2(1f, 0f), new Vector2(-92f, 62f), 28f, "<Keyboard>/leftShift");
            Button(rect, "Crouch", new Vector2(1f, 0f), new Vector2(-40f, 44f), 28f, "<Keyboard>/leftCtrl");
            Button(rect, "Torch", new Vector2(1f, 1f), new Vector2(-40f, -40f), 28f, "<Keyboard>/f");

            var controls = group.AddComponent<TouchControls>();
            var so = new SerializedObject(controls);
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            group.SetActive(false);
        }

        private static void Stick(Transform parent, string name, Vector2 anchor, Vector2 offset,
                                  string controlPath)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(OnScreenStick));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(52f, 52f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.85f, 0.85f, 0.80f, 0.22f);

            var stick = go.GetComponent<OnScreenStick>();
            var so = new SerializedObject(stick);
            so.FindProperty("m_ControlPath").stringValue = controlPath;
            so.FindProperty("m_MovementRange").floatValue = 26f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Button(Transform parent, string name, Vector2 anchor, Vector2 offset,
                                   float size, string controlPath)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(OnScreenButton));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(size, size);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.85f, 0.85f, 0.80f, 0.20f);

            Text label = Label(rect, "Label", name[..1], 7, Vector2.zero);
            label.color = new Color(0.95f, 0.93f, 0.86f, 0.75f);

            var button = go.GetComponent<OnScreenButton>();
            var so = new SerializedObject(button);
            so.FindProperty("m_ControlPath").stringValue = controlPath;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text Label(Transform parent, string name, string content, int size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(400f, size + 8f);

            Text text = go.GetComponent<Text>();
            text.font = PixelFontBuilder.Load();
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.90f, 0.86f, 0.80f, 0.95f);
            text.raycastTarget = false;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BuildReticle(Transform parent)
        {
            var group = new GameObject("Interaction", typeof(RectTransform));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;

            var dot = new GameObject("Reticle", typeof(RectTransform), typeof(Image));
            var dotRect = (RectTransform)dot.transform;
            dotRect.SetParent(rect, false);
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(3f, 3f);
            Image reticle = dot.GetComponent<Image>();
            reticle.raycastTarget = false;

            var text = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)text.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -14f);   // just under the reticle
            textRect.sizeDelta = new Vector2(220f, 20f);

            Text label = text.GetComponent<Text>();
            // The project's own bitmap font. A vector font rasterised into a 360p buffer lands
            // on fractional pixels, comes out grey, and is then magnified by the point upscale.
            label.font = PixelFontBuilder.Load();
            label.fontSize = 12;
            label.alignment = TextAnchor.UpperCenter;
            label.color = new Color(0.92f, 0.90f, 0.80f, 0.95f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var view = group.AddComponent<InteractionPromptView>();
            var so = new SerializedObject(view);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("reticle").objectReferenceValue = reticle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
