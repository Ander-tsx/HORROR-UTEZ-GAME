using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UtezHorror.Player;

namespace UtezHorror.EditorTools
{
    public static class CecadecDemoBuilder
    {
        private const string FbxPath = "Assets/_Project/Art/Models/Environment/Cecadec/CecadecDemo.fbx";
        private const string ScenePath = "Assets/_Project/Scenes/Cecadec_Demo.unity";
        private const string TextureDir = "Assets/_Project/Art/Textures/Procedural";

        [MenuItem("Tools/UtezHorror/Build Cecadec Demo")]
        public static void Build()
        {
            ConfigureImporter();
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            ApplyMaterials();
            BuildScene();
            Debug.Log("CecadecDemoBuilder: build complete");
        }

        private static void ConfigureImporter()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private struct MatSpec
        {
            public string Name;
            public Color Color;
            public float Metallic;
            public float Smoothness;
            public bool Noise;
            public float NoiseScaleX;
            public float NoiseScaleY;
            public float NoiseVariation;
            public Vector2 Tiling;
        }

        private static void ApplyMaterials()
        {
            Directory.CreateDirectory(TextureDir);

            var specs = new List<MatSpec>
            {
                new MatSpec { Name = "Mat_WallPaint", Color = new Color(0.80f, 0.78f, 0.72f), Smoothness = 0.25f, Noise = true, NoiseScaleX = 0.06f, NoiseScaleY = 0.06f, NoiseVariation = 0.10f, Tiling = new Vector2(3f, 1.5f) },
                new MatSpec { Name = "Mat_Floor", Color = new Color(0.62f, 0.61f, 0.58f), Smoothness = 0.35f, Noise = true, NoiseScaleX = 0.20f, NoiseScaleY = 0.20f, NoiseVariation = 0.08f, Tiling = new Vector2(1f, 1f) },
                new MatSpec { Name = "Mat_FloorGrout", Color = new Color(0.30f, 0.29f, 0.28f), Smoothness = 0.15f },
                new MatSpec { Name = "Mat_Ceiling", Color = new Color(0.92f, 0.92f, 0.90f), Smoothness = 0.2f },
                new MatSpec { Name = "Mat_Door", Color = new Color(0.42f, 0.27f, 0.16f), Smoothness = 0.3f, Noise = true, NoiseScaleX = 0.04f, NoiseScaleY = 0.4f, NoiseVariation = 0.14f, Tiling = new Vector2(1f, 1f) },
                new MatSpec { Name = "Mat_DoorFrame", Color = new Color(0.22f, 0.14f, 0.09f), Smoothness = 0.25f },
                new MatSpec { Name = "Mat_FurnitureWood", Color = new Color(0.55f, 0.38f, 0.22f), Smoothness = 0.35f, Noise = true, NoiseScaleX = 0.05f, NoiseScaleY = 0.45f, NoiseVariation = 0.16f, Tiling = new Vector2(1f, 1f) },
                new MatSpec { Name = "Mat_FurnitureMetal", Color = new Color(0.60f, 0.60f, 0.63f), Metallic = 0.6f, Smoothness = 0.65f },
                new MatSpec { Name = "Mat_PC", Color = new Color(0.07f, 0.07f, 0.08f), Metallic = 0.2f, Smoothness = 0.4f },
                new MatSpec { Name = "Mat_Stair", Color = new Color(0.50f, 0.50f, 0.52f), Smoothness = 0.2f, Noise = true, NoiseScaleX = 0.15f, NoiseScaleY = 0.15f, NoiseVariation = 0.08f, Tiling = new Vector2(1f, 1f) },
                new MatSpec { Name = "Mat_Roof", Color = new Color(0.32f, 0.32f, 0.35f), Smoothness = 0.2f },
                new MatSpec { Name = "Mat_Board", Color = new Color(0.95f, 0.95f, 0.93f), Smoothness = 0.5f },
            };

            foreach (var spec in specs)
            {
                var guids = AssetDatabase.FindAssets($"{spec.Name} t:Material");
                Material mat = null;
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == spec.Name)
                    {
                        mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        break;
                    }
                }

                if (mat == null)
                {
                    Debug.LogWarning($"CecadecDemoBuilder: material {spec.Name} not found after import, skipping");
                    continue;
                }

                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                mat.SetColor("_BaseColor", spec.Color);
                mat.SetFloat("_Metallic", spec.Metallic);
                mat.SetFloat("_Smoothness", spec.Smoothness);

                if (spec.Noise)
                {
                    var tex = BuildNoiseTexture(spec.Name, spec.Color, spec.NoiseVariation, spec.NoiseScaleX, spec.NoiseScaleY);
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTextureScale("_BaseMap", spec.Tiling);
                }

                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
        }

        private static Texture2D BuildNoiseTexture(string matName, Color baseColor, float variation, float scaleX, float scaleY)
        {
            const int size = 128;
            var assetPath = $"{TextureDir}/{matName}_noise.png";

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * scaleX, y * scaleY);
                    float shade = 1f + (n - 0.5f) * variation;
                    var c = new Color(
                        Mathf.Clamp01(baseColor.r * shade),
                        Mathf.Clamp01(baseColor.g * shade),
                        Mathf.Clamp01(baseColor.b * shade),
                        1f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabRoot);
            instance.name = "Cecadec";

            var structureTransform = FindDeepChild(instance.transform, "CecadecDemo_Structure");
            var furnitureTransform = FindDeepChild(instance.transform, "CecadecDemo_Furniture");

            CorrectImportScale(instance, structureTransform);

            Debug.Log($"CecadecDemoBuilder: instance transform pos={instance.transform.position}, rot={instance.transform.rotation.eulerAngles}, scale={instance.transform.localScale}");
            if (structureTransform != null)
            {
                Debug.Log($"CecadecDemoBuilder: structureTransform world pos={structureTransform.position}, lossyScale={structureTransform.lossyScale}, localPos={structureTransform.localPosition}");
            }

            var structureCollider = AddColliderTo(structureTransform);
            var furnitureCollider = AddColliderTo(furnitureTransform);
            if (structureCollider != null)
            {
                Debug.Log($"CecadecDemoBuilder: structureCollider bounds center={structureCollider.bounds.center}, size={structureCollider.bounds.size}, go pos={structureCollider.transform.position}, scale={structureCollider.transform.localScale}");
            }

            AddLights();
            var playerPos = FindSpawnPoint(structureCollider);
            AddPlayer(playerPos);
            Debug.Log($"CecadecDemoBuilder: player spawn at {playerPos}");

            EditorSceneManager.SaveScene(scene, ScenePath);

            var existing = EditorBuildSettings.scenes.ToList();
            if (!existing.Any(s => s.path == ScenePath))
            {
                existing.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = existing.ToArray();
            }
        }

        // Blender hallway spans local X from -0.2 to HALL_LEN(25.0); used to detect and
        // correct FBX import scale mismatches (Blender/Unity meter conversion is fragile).
        private const float ExpectedHallLength = 25.2f;

        private static void CorrectImportScale(GameObject instance, Transform structureTransform)
        {
            if (structureTransform == null)
            {
                Debug.LogWarning("CecadecDemoBuilder: structure transform missing, cannot check import scale");
                return;
            }
            var meshFilter = structureTransform.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning("CecadecDemoBuilder: structure mesh missing, cannot check import scale");
                return;
            }
            // Use local mesh bounds * lossyScale instead of Renderer.bounds: Renderer.bounds can be
            // stale/zero in a headless batch run before any render pass has occurred.
            float localLength = meshFilter.sharedMesh.bounds.size.x;
            float measuredWorldLength = localLength * structureTransform.lossyScale.x;
            Debug.Log($"CecadecDemoBuilder: structure local length {localLength:F3}, lossyScale.x {structureTransform.lossyScale.x:F4}, measured world length {measuredWorldLength:F3}m (expected {ExpectedHallLength}m)");
            if (measuredWorldLength < 0.01f) return;
            float factor = ExpectedHallLength / measuredWorldLength;
            if (Mathf.Abs(factor - 1f) > 0.05f)
            {
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, new Vector3(factor, factor, factor));
                Debug.Log($"CecadecDemoBuilder: corrected import scale by factor {factor:F4}");
            }
        }

        // The join in Blender leaves the exported object's pivot wherever the "active" object
        // happened to be during bpy.ops.object.join(), and the Blender->Unity axis conversion can
        // flip signs — so authored coordinates like "hallway starts near x=0.5" do NOT reliably
        // correspond to the same numbers after import. Instead of a hardcoded spawn position, pick a
        // point inside the actual baked collider's footprint (biased toward one end and toward the
        // hallway side, not the room depth) and raycast straight down to land exactly on the floor.
        private static Vector3 FindSpawnPoint(MeshCollider structureCollider)
        {
            var fallback = new Vector3(0.5f, 1f, 1.5f);
            if (structureCollider == null) return fallback;

            var b = structureCollider.bounds;
            float x = b.max.x - 1.0f;
            float z = b.max.z - 1.5f;
            // Starting above the whole building hits the roof first (it's a single closed mesh with
            // no interior gaps to fall through visually) — start just under the ground-floor ceiling
            // height instead, so the ray is already inside the hallway airspace and only has to drop
            // a couple meters onto the ground-floor tile.
            var rayStart = new Vector3(x, b.min.y + 2.0f, z);

            if (Physics.Raycast(rayStart, Vector3.down, out var hit, 10f))
            {
                Debug.Log($"CecadecDemoBuilder: spawn raycast hit {hit.collider.name} at {hit.point}");
                return hit.point + Vector3.up * 0.05f;
            }

            Debug.LogWarning("CecadecDemoBuilder: spawn raycast found nothing, falling back to bounds-based estimate");
            return new Vector3(x, b.min.y + 1f, z);
        }

        // The FBX import ends up with an extreme, oddly-distributed scale (tiny local mesh x huge
        // transform scale) that nets out to the right world size, but PhysX's mesh cooking uses
        // scale-dependent welding tolerances tuned for human-scale meshes: cooking a mesh whose local
        // coordinates only span ~1cm (even though the *displayed* result is ~25m) can silently weld
        // together vertices that are fractions of a millimeter apart in that tiny local space,
        // corrupting the collider topology (holes the player falls through). Fix: bake each mesh's
        // vertices into real world-space coordinates on a fresh, identity-scaled GameObject before
        // handing it to MeshCollider, so PhysX cooks it at its actual ~25m size.
        private static MeshCollider AddColliderTo(Transform found)
        {
            if (found == null)
            {
                Debug.LogWarning("CecadecDemoBuilder: child not found for collider setup");
                return null;
            }
            var meshFilter = found.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"CecadecDemoBuilder: '{found.name}' has no mesh, skipping collider");
                return null;
            }

            var source = meshFilter.sharedMesh;
            var worldVerts = new Vector3[source.vertexCount];
            var matrix = found.localToWorldMatrix;
            var sourceVerts = source.vertices;
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (int i = 0; i < sourceVerts.Length; i++)
            {
                worldVerts[i] = matrix.MultiplyPoint3x4(sourceVerts[i]);
                min = Vector3.Min(min, worldVerts[i]);
                max = Vector3.Max(max, worldVerts[i]);
            }
            Debug.Log($"CecadecDemoBuilder: '{found.name}' local mesh bounds={source.bounds}, baked world AABB min={min}, max={max}, size={max - min}");

            var bakedMesh = new Mesh { name = source.name + "_ColliderBaked" };
            bakedMesh.indexFormat = source.indexFormat;
            bakedMesh.vertices = worldVerts;
            bakedMesh.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                bakedMesh.SetTriangles(source.GetTriangles(i), i);
            }
            bakedMesh.RecalculateBounds();
            bakedMesh.RecalculateNormals();

            // Left unparented at the scene root with an identity transform: the baked vertices are
            // already in absolute world space, so this GameObject's localToWorldMatrix must stay
            // identity or the mesh would be transformed a second time.
            var colliderGo = new GameObject(found.name + "_Collider");
            colliderGo.transform.position = Vector3.zero;
            colliderGo.transform.rotation = Quaternion.identity;
            colliderGo.transform.localScale = Vector3.one;

            var collider = colliderGo.AddComponent<MeshCollider>();
            collider.sharedMesh = bakedMesh;
            collider.convex = false;
            return collider;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static void AddLights()
        {
            var hallwayXs = new[] { 2f, 8f, 14f, 20f };
            var floorYs = new[] { 2.6f, 3.2f + 2.6f };
            foreach (var fy in floorYs)
            {
                foreach (var x in hallwayXs)
                {
                    CreatePointLight($"HallLight_{fy}_{x}", new Vector3(x, fy, 1.5f), 6f, 1.2f);
                }

                var roomXs = new[] { 3.5f, 9.5f, 15.5f };
                foreach (var x in roomXs)
                {
                    CreatePointLight($"RoomLight_{fy}_{x}", new Vector3(x, fy, 6f), 7f, 1.3f);
                }
            }
        }

        private static void CreatePointLight(string name, Vector3 position, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.96f, 0.88f);
        }

        private static void AddPlayer(Vector3 position)
        {
            var player = new GameObject("Player");
            player.transform.position = position;
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
        }
    }
}
