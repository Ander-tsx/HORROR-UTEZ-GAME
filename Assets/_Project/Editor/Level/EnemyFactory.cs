using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UtezHorror.Enemies;
using UtezHorror.Utils;
using static UtezHorror.EditorTools.Level.CecadecLayout;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Places the building's one professor.
    ///
    /// One, not several: Docs/Plans/01 cuts the MVP down to a single enemy that chases well,
    /// on the grounds that one convincing threat is worth more than three vague ones and costs
    /// a third as much to tune. A second is an <see cref="EnemyConfig"/> asset and another call
    /// here, not new code.
    ///
    /// The body is a rigged model, decimated from a downloaded sculpt by
    /// <c>Tools/Blender/make_enemy.py</c> (211,755 triangles down to 2,599, rigged from scratch
    /// because the source had no skeleton). If that model is ever missing, it falls back to the
    /// boxes it started as — a level that builds without an enemy is worse than an ugly one.
    /// </summary>
    public static class EnemyFactory
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/EnemyConfigs/Vigilante.asset";
        private const string ModelPath = "Assets/_Project/Art/Models/Characters/Enemies/Profesor.blend";
        private const string TexturePath = "Assets/_Project/Art/Textures/Characters/Profesor_BaseColor.png";

        public static GameObject Build(Transform parent)
        {
            EnemyConfig config = EnsureConfig();

            var enemy = new GameObject("Enemy_Vigilante") { layer = GameLayers.Enemy };
            enemy.transform.SetParent(parent, false);

            List<Vector3> route = PatrolRoute();
            enemy.transform.position = route[0];

            GameObject body = BuildModelBody(enemy.transform);
            if (body == null) BuildBoxBody(enemy.transform);

            // Slightly narrower than a person so it fits the 1.15 m doorways without clipping
            // the frame, and a step height that clears the stair treads.
            var agent = enemy.AddComponent<NavMeshAgent>();
            agent.radius = 0.34f;
            agent.height = 1.85f;
            agent.baseOffset = 0f;
            agent.speed = config.patrolSpeed;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;
            // Enemy-Enemy physics is off in the collision matrix so avoidance does not fight it,
            // but with one enemy there is nothing to avoid anyway.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.height = 1.85f;
            capsule.radius = 0.34f;
            capsule.center = new Vector3(0f, 0.925f, 0f);

            var hearing = enemy.AddComponent<EnemyHearing>();
            SetRef(hearing, "config", config);

            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(enemy.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 1.62f, 0.1f);

            var vision = enemy.AddComponent<EnemyVision>();
            SetRef(vision, "config", config);
            SetRef(vision, "eyes", eyes.transform);

            var ai = enemy.AddComponent<EnemyAI>();
            SetRef(ai, "config", config);
            SetRef(ai, "hearing", hearing);
            SetRef(ai, "vision", vision);
            SetRoute(ai, route);

            if (body != null)
            {
                var anim = enemy.AddComponent<EnemyAnimator>();
                SetRef(anim, "ai", ai);
                SetRef(anim, "agent", agent);
                SetRef(anim, "animator", body.GetComponentInChildren<Animator>());
            }

            Debug.Log($"[Enemy] Placed '{config.displayName}' on a {route.Count}-point patrol.");
            return enemy;
        }

        /// <summary>
        /// The route, derived from the layout rather than placed by hand.
        ///
        /// That matters because the real building plan is still being surveyed: when the cotas
        /// change, these points move with them instead of ending up inside a wall. Every point
        /// is a corridor centre, so the professor walks where a person would and the player
        /// meets it head-on rather than having it appear from inside a classroom.
        /// </summary>
        private static List<Vector3> PatrolRoute()
        {
            float corridorX = (XWestBlock + XEastBlock) * 0.5f;
            float crossZ = (ZSouthRooms + ZCrossNorth) * 0.5f;
            float upper = FloorToFloor;

            // Both floors. The first route stayed downstairs, so the professor only ever went up
            // while already chasing — which is exactly what "it goes upstairs, but only
            // sometimes" looks like from the corridor. A patrol that never visits half the
            // building makes that half permanently safe.
            var stair = new Vector3(StairCentre.x + 2.6f, 0f, crossZ);

            return new List<Vector3>
            {
                new(corridorX, 0f, ZNorth - 5f),        // north end, by the main doors
                new(corridorX, 0f, ZRoomSplit),         // outside the aula normal
                new(corridorX, 0f, ZCrossNorth + 3f),   // outside the centro de cómputo
                new(corridorX, 0f, crossZ),             // the junction
                new(XEast - 5f, 0f, crossZ),            // east arm of the cross corridor
                new(corridorX, 0f, ZSouthRooms - 4f),   // south corridor
                new(corridorX, 0f, crossZ),
                stair,                                  // to the foot of the stair

                new(StairCentre.x + 2.6f, upper, crossZ),   // and up
                new(corridorX, upper, crossZ),
                new(corridorX, upper, ZCrossNorth + 4f),
                new(corridorX, upper, ZRoomSplit),
                new(corridorX, upper, ZNorth - 6f),         // far end of the upper corridor
                new(corridorX, upper, crossZ),
                new(StairCentre.x + 2.6f, upper, crossZ),   // and back down
                stair
            };
        }

        /// <summary>
        /// The rigged creature. Returns null if the model has not been generated yet.
        ///
        /// The import rotation is left exactly as it arrives. An earlier version added 180° on Y
        /// "to make it face forwards" and that is what made the professor charge at the player
        /// backwards: the model already faces -Y in Blender, which the importer's axis conversion
        /// turns into Unity's +Z — the same direction a NavMeshAgent walks. The correction was
        /// the bug.
        /// </summary>
        private static GameObject BuildModelBody(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Enemy] No model at {ModelPath}; using the box placeholder. " +
                                 "Regenerate with Tools/Blender/make_enemy.py.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "Body";
            instance.transform.localPosition = Vector3.zero;

            // Tinted down and warmed rather than left at white. The source texture is a pale
            // grey skin, and multiplied by white under a torch it read as a lit marble statue
            // instead of something standing in a dark corridor. It still comes out lighter than
            // every surface around it, which is what makes it register instantly.
            Material skin = LevelPrimitives.Mat("EnemySkin", new Color(0.58f, 0.55f, 0.50f), 0.05f,
                                                baseMap: AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = skin;
                // A skinned mesh that casts shadows is the cheapest way to make something read as
                // physically present in a corridor lit by one torch.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            SetLayerRecursively(instance.transform, GameLayers.Enemy);
            return instance;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursively(child, layer);
        }

        /// <summary>Fallback body: a head, a torso and legs. Silhouette only.</summary>
        private static void BuildBoxBody(Transform parent)
        {
            // Darker than the walls at every value, so it reads as a hole in the torch beam
            // rather than as another grey surface.
            Material cloth = LevelPrimitives.Mat("EnemyCloth", new Color(0.22f, 0.24f, 0.22f), 0.04f,
                                                 baseMap: LevelTextures.Wall,
                                                 triplanar: true, triplanarScale: 0.9f);
            Material skin = LevelPrimitives.Mat("EnemySkin", new Color(0.62f, 0.56f, 0.48f), 0.05f,
                                                baseMap: LevelTextures.Wood,
                                                triplanar: true, triplanarScale: 0.5f);

            var group = LevelPrimitives.Group(parent, "Body");
            group.isStatic = false;
            int layer = GameLayers.Enemy;

            Box(group.transform, "Legs", new Vector3(0f, 0.44f, 0f), new Vector3(0.34f, 0.88f, 0.26f), cloth, layer);
            Box(group.transform, "Torso", new Vector3(0f, 1.20f, 0f), new Vector3(0.46f, 0.64f, 0.28f), cloth, layer);
            Box(group.transform, "ArmL", new Vector3(-0.30f, 1.16f, 0f), new Vector3(0.13f, 0.62f, 0.16f), cloth, layer);
            Box(group.transform, "ArmR", new Vector3(0.30f, 1.16f, 0f), new Vector3(0.13f, 0.62f, 0.16f), cloth, layer);
            Box(group.transform, "Head", new Vector3(0f, 1.65f, 0f), new Vector3(0.22f, 0.26f, 0.22f), skin, layer);
        }

        private static void Box(Transform parent, string name, Vector3 centre, Vector3 size,
                                Material material, int layer)
        {
            // No colliders on the parts: the agent's capsule is the enemy's physical presence,
            // and extra colliders would also fight the interaction raycast.
            GameObject go = LevelPrimitives.Box(parent, name, centre, size, material, layer,
                                                collider: false, tiling: 0.9f);
            go.isStatic = false;
        }

        private static EnemyConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(ConfigPath);
            if (config != null) return config;

            LevelPrimitives.EnsureFolder("Assets/_Project/ScriptableObjects/EnemyConfigs");
            config = ScriptableObject.CreateInstance<EnemyConfig>();
            config.displayName = "El Vigilante";
            config.designNotes =
                "Patrulla los pasillos a ritmo constante. No es rápido, es persistente: te oye " +
                "correr desde lejos y no se rinde pronto. El primero, y el que enseña las reglas.";
            config.patrolSpeed = 1.5f;
            config.chaseSpeed = 4.0f;
            config.sightRange = 16f;
            config.sightAngle = 95f;
            config.hearingMultiplier = 1.3f;
            config.catchRadius = 1.3f;
            config.searchDuration = 14f;
            config.earliestPhase = Core.ShiftPhase.Arrival;

            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[Enemy] {target.GetType().Name} has no field '{property}'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRoute(EnemyAI ai, List<Vector3> route)
        {
            var so = new SerializedObject(ai);
            SerializedProperty list = so.FindProperty("patrolPoints");
            list.arraySize = route.Count;
            for (int i = 0; i < route.Count; i++)
                list.GetArrayElementAtIndex(i).vector3Value = route[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
