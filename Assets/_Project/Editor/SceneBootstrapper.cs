using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UtezHorror.Utils;

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
            floor.layer = GameLayers.Environment;

            CreateWall("Wall_North", new Vector3(0f, 1.5f, 10f), new Vector3(20f, 3f, 0.2f));
            CreateWall("Wall_South", new Vector3(0f, 1.5f, -10f), new Vector3(20f, 3f, 0.2f));
            CreateWall("Wall_East", new Vector3(10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f));
            CreateWall("Wall_West", new Vector3(-10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f));

            PlayerRigBuilder.Build(new Vector3(0f, 1f, 0f));
            PlayerRigBuilder.BuildGameManager();

            SceneLightingSetup.ApplyInterior();

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettingsScene[] buildScenes = { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = buildScenes;
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.layer = GameLayers.Environment;
            wall.transform.position = position;
            wall.transform.localScale = scale;
        }
    }
}
