using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UtezHorror.Player;

namespace UtezHorror.EditorTools
{
    public static class SceneBootstrapper
    {
        private const string UrpAssetPath = "Assets/Settings/UtezHorror_URP.asset";
        private const string UrpRendererPath = "Assets/Settings/UtezHorror_Renderer.asset";
        private const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";

        public static void Setup()
        {
            SetupUrp();
            BuildGreyboxScene();
            Debug.Log("SceneBootstrapper: setup complete");
        }

        private static void SetupUrp()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, UrpRendererPath);
            }

            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urpAsset, UrpAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;
            AssetDatabase.SaveAssets();
        }

        private static void BuildGreyboxScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.intensity = 0.4f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
            floor.transform.position = new Vector3(0f, -0.1f, 0f);

            CreateWall("Wall_North", new Vector3(0f, 1.5f, 10f), new Vector3(20f, 3f, 0.2f));
            CreateWall("Wall_South", new Vector3(0f, 1.5f, -10f), new Vector3(20f, 3f, 0.2f));
            CreateWall("Wall_East", new Vector3(10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f));
            CreateWall("Wall_West", new Vector3(-10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f));

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.35f;

            var cameraGo = new GameObject("PlayerCamera");
            cameraGo.transform.SetParent(player.transform);
            cameraGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            camera.tag = "MainCamera";

            var fpsController = player.AddComponent<FirstPersonController>();
            var so = new SerializedObject(fpsController);
            so.FindProperty("playerCamera").objectReferenceValue = camera;
            so.ApplyModifiedProperties();

            var playerInput = player.AddComponent<PlayerInput>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Project/Input/PlayerControls.inputactions");
            playerInput.actions = actions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettingsScene[] buildScenes = { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = buildScenes;
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
        }
    }
}
