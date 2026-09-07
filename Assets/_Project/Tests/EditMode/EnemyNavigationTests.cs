using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UtezHorror.Enemies;
using UtezHorror.Interaction;
using static UtezHorror.EditorTools.Level.CecadecLayout;

namespace UtezHorror.Tests
{
    /// <summary>
    /// Cover for the things that make an enemy silently useless rather than visibly broken.
    ///
    /// An enemy off the NavMesh does not throw — it stands still and logs a pathfinding error
    /// every frame, which in a dark corridor is indistinguishable from "the AI has not spawned
    /// yet". A patrol point inside a wall does the same. Both are exactly the sort of thing a
    /// layout change causes, and the real building plan is still being surveyed, so this will
    /// earn its keep.
    /// </summary>
    public sealed class EnemyNavigationTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Cecadec.unity";

        /// <summary>How far off the mesh a point may be and still be considered placed on it.</summary>
        private const float SampleRadius = 1.5f;

        private Scene scene;

        [OneTimeSetUp]
        public void OpenScene()
        {
            Assume.That(System.IO.File.Exists(ScenePath), "Cecadec scene has not been built yet.");
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, removeScene: true);
        }

        private static EnemyAI Enemy =>
            Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .FirstOrDefault();

        [Test]
        public void TheBuildingHasExactlyOneEnemy()
        {
            EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None);
            // Docs/Plans/01 cuts the MVP to one enemy that chases well. More than one here means
            // somebody added a second without the tuning budget that decision was protecting.
            Assert.AreEqual(1, enemies.Length, "Expected exactly one enemy in the level.");
        }

        [Test]
        public void NavMeshCoversTheBuilding()
        {
            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            Assert.Greater(mesh.vertices.Length, 100,
                "The NavMesh is empty or missing; the enemy cannot move anywhere. " +
                "Rebuild with Tools > UtezHorror > Apply PS1 Look and Rebuild Level.");
        }

        [Test]
        public void EnemyStandsOnTheNavMesh()
        {
            EnemyAI enemy = Enemy;
            Assert.IsNotNull(enemy, "No enemy in the level.");

            Assert.IsTrue(
                NavMesh.SamplePosition(enemy.transform.position, out _, SampleRadius, NavMesh.AllAreas),
                $"Enemy spawn {enemy.transform.position} is off the NavMesh. It will stand still " +
                "and log a pathfinding error every frame. Check that the bake runs before the " +
                "enemy is placed.");
        }

        [Test]
        public void EveryPatrolPointIsOnTheNavMesh()
        {
            EnemyAI enemy = Enemy;
            Assert.IsNotNull(enemy, "No enemy in the level.");

            var so = new UnityEditor.SerializedObject(enemy);
            UnityEditor.SerializedProperty points = so.FindProperty("patrolPoints");
            Assert.Greater(points.arraySize, 2, "A patrol needs more than a couple of points.");

            for (int i = 0; i < points.arraySize; i++)
            {
                Vector3 point = points.GetArrayElementAtIndex(i).vector3Value;
                Assert.IsTrue(NavMesh.SamplePosition(point, out _, SampleRadius, NavMesh.AllAreas),
                              $"Patrol point {i} at {point} is not on the NavMesh — probably inside " +
                              "a wall after a layout change. Patrol routes come from CecadecLayout.");
            }
        }

        [Test]
        public void TheEnemyCanReachTheUpperFloor()
        {
            // The stair is the only link between floors. If it does not bake, the enemy is
            // confined downstairs and half the building is permanently safe — which reads as
            // the upper floor being finished rather than broken.
            float corridorX = (XWestBlock + XEastBlock) * 0.5f;
            var ground = new Vector3(corridorX, 0f, ZNorth - 5f);
            var upper = new Vector3(corridorX, FloorToFloor, ZNorth - 5f);

            Assert.IsTrue(NavMesh.SamplePosition(ground, out NavMeshHit from, 2f, NavMesh.AllAreas),
                          "Ground floor corridor is not walkable.");
            Assert.IsTrue(NavMesh.SamplePosition(upper, out NavMeshHit to, 2f, NavMesh.AllAreas),
                          "Upper floor corridor is not walkable.");

            var path = new NavMeshPath();
            NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);

            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                            "No complete path from the ground floor to the upper floor. The stair " +
                            "did not bake: check tread rise against the agent's step height.");
        }

        [Test]
        public void BatteriesAreScarceAndReachable()
        {
            BatteryPickup[] batteries = Object.FindObjectsByType<BatteryPickup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Scarcity is the design, not a placeholder: the torch is the run's only consumable
            // and running out is what keeps the player moving. See CecadecLevelBuilder.Batteries.
            Assert.That(batteries.Length, Is.InRange(3, 8),
                        "Battery count has drifted away from 'very scarce'.");

            foreach (BatteryPickup battery in batteries)
            {
                Assert.IsTrue(
                    NavMesh.SamplePosition(battery.transform.position, out _, 3f, NavMesh.AllAreas),
                    $"Battery '{battery.name}' at {battery.transform.position} has no walkable " +
                    "ground within reach, so the player cannot get to it.");
            }
        }

        /// <summary>
        /// The enemy must be the rigged creature, not the box placeholder it falls back to.
        ///
        /// The fallback exists so a missing model cannot break the build, which also means it
        /// fails quietly: a warning in a log nobody reads, and an enemy made of five grey boxes.
        /// This turns that into a red test.
        /// </summary>
        [Test]
        public void TheEnemyUsesTheRiggedModel()
        {
            EnemyAI enemy = Enemy;
            Assert.IsNotNull(enemy, "No enemy in the level.");

            var skinned = enemy.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(skinned,
                "The enemy has no SkinnedMeshRenderer, so it fell back to the box placeholder. " +
                "Regenerate the model with Tools/Blender/make_enemy.py.");

            Assert.Greater(skinned.bones.Length, 10,
                $"Only {skinned.bones.Length} bones; the rig did not survive the import.");

            Assert.IsNotNull(enemy.GetComponent<EnemyAnimator>(),
                "No EnemyAnimator, so the creature slides down the corridor without moving its legs.");
        }
    }
}
