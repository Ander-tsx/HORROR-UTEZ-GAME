using UnityEditor;
using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Interaction;
using UtezHorror.Player;
using UtezHorror.Utils;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Single source of truth for how a player is assembled. Both scene builders call this,
    /// so the rig can never drift between the greybox and the Cecadec scene.
    /// </summary>
    public static class PlayerRigBuilder
    {
        public const float EyeHeight = 1.62f;

        public static GameObject Build(Vector3 position)
        {
            var player = new GameObject("Player") { layer = GameLayers.Player };
            player.transform.position = position;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.35f;
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;      // enough to walk the Cecadec stair treads
            controller.skinWidth = 0.03f;

            var cameraGo = new GameObject("PlayerCamera");
            cameraGo.transform.SetParent(player.transform);
            cameraGo.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;       // stops geometry clipping when hugging a wall
            camera.farClipPlane = 90f;          // reaches CDS across the wood; fog hides the cut
            camera.clearFlags = CameraClearFlags.SolidColor;
            // AtmosphereController keeps this matching the fog every frame; this is only the
            // value the scene is saved with.
            camera.backgroundColor = UtezHorror.Environment.AtmospherePresets.ExteriorEarly.fogColor;
            cameraGo.AddComponent<AudioListener>();
            // Everything the player hears goes through the crusher, including sounds that do not
            // exist yet — which is the point of adding it now: it decides how audio gets authored.
            cameraGo.AddComponent<UtezHorror.Audio.RetroAudioFilter>();

            var beamGo = new GameObject("PhoneTorch");
            beamGo.transform.SetParent(cameraGo.transform);
            beamGo.transform.localPosition = Vector3.zero;
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            // A narrow cone that lights only what is directly ahead and leaves the corners of
            // the screen black — the brief for the PS1 pass. It was 50°/22° at intensity 15.
            //
            // The intensity more than doubled at the same time, which looks wrong until you
            // account for what changed underneath it: the building used to be flat near-white
            // material at roughly 0.86 albedo and is now textured and dirty at roughly 0.24, so
            // the same beam returns about a third of the light it used to. Narrowing the cone
            // then concentrated what was left onto a smaller patch. 34 puts the lit area back
            // where 15 had it, with a tighter beam and much darker surroundings.
            beam.range = 26f;
            beam.spotAngle = 40f;
            beam.innerSpotAngle = 14f;
            beam.intensity = 34f;
            beam.color = new Color(0.92f, 0.94f, 1f);
            beam.shadows = LightShadows.Hard;
            beam.enabled = false;

            var interactor = player.AddComponent<PlayerInteractor>();
            SetRef(interactor, "rayOrigin", cameraGo.transform);

            var router = player.AddComponent<PlayerInputRouter>();
            SetRef(router, "interactor", interactor);

            var fps = player.AddComponent<FirstPersonController>();
            SetRef(fps, "playerCamera", camera);

            var torch = player.AddComponent<PlayerFlashlight>();
            SetRef(torch, "beam", beam);

            player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerThief>();
            player.AddComponent<PlayerVisibility>();
            player.AddComponent<PlayerDeath>();
            player.AddComponent<UtezHorror.Audio.PlayerAudio>();

            // Parented to the camera so the HUD travels with the view and, more importantly, so
            // its canvas renders through the camera — which is what puts it inside the 360p
            // buffer instead of on top of it at native resolution.
            HudBuilder.Build(camera).transform.SetParent(cameraGo.transform, false);

            return player;
        }

        /// <summary>Creates the scene-level clock if the scene has no game manager yet.</summary>
        public static GameObject BuildGameManager()
        {
            var go = new GameObject("GameManager");
            var clock = go.AddComponent<GameClock>();

            var config = AssetDatabase.LoadAssetAtPath<ShiftConfig>(
                "Assets/_Project/ScriptableObjects/ShiftConfig.asset");
            if (config != null) SetRef(clock, "config", config);
            else Debug.LogWarning("[PlayerRigBuilder] ShiftConfig.asset not found; clock left unconfigured.");

            go.AddComponent<PowerSystem>();
            go.AddComponent<UtezHorror.Environment.AtmosphereController>();
            go.AddComponent<EventDirector>();

            var objectives = go.AddComponent<ObjectiveSystem>();
            var so = new SerializedObject(objectives);
            SerializedProperty list = so.FindProperty("required");
            ObjectiveItem[] items = Level.ObjectiveCatalog.EnsureAll();
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[PlayerRigBuilder] {target.GetType().Name} has no field '{property}'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
