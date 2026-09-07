using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UtezHorror.EditorTools.Level;
using UtezHorror.Interaction;
using UtezHorror.Utils;
using static UtezHorror.EditorTools.Level.CecadecLayout;

namespace UtezHorror.Tests
{
    /// <summary>
    /// Regression cover for the Cecadec level. The first build of this building shipped with
    /// every ceiling fixture sitting on the lawn, because light positions came from design
    /// coordinates while the geometry had been moved by the FBX pipeline. These tests fail if
    /// that class of mistake comes back, and they walk the stairs to prove both floors connect.
    /// </summary>
    public sealed class CecadecLevelTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Cecadec.unity";

        private static float TopOfBuilding => CecadecLevelBuilder.FloorCount * FloorToFloor + CeilingHeight;

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

        private static Transform Root => GameObject.Find("Cecadec")?.transform;

        // ---------------------------------------------------------------- structure

        [Test]
        public void BothFloorsExist()
        {
            Assert.IsNotNull(Root, "Level root 'Cecadec' is missing.");
            for (int i = 1; i <= CecadecLevelBuilder.FloorCount; i++)
                Assert.IsNotNull(Root.Find($"Floor{i}"), $"Floor{i} is missing from the scene.");
        }

        [Test]
        public void EachFloorIsFurnishedTheSameWay()
        {
            Transform f1 = Root.Find("Floor1/Furniture");
            Transform f2 = Root.Find("Floor2/Furniture");
            Assert.IsNotNull(f1, "Floor1 has no furniture.");
            Assert.IsNotNull(f2, "Floor2 has no furniture.");

            // Batteries are excluded: they are the one thing deliberately *not* mirrored, so
            // that taking the stair is worth doing. Everything else is the same code running
            // with a different parent, and a mismatch means that duplication path broke.
            Assert.AreEqual(CountFurniture(f1), CountFurniture(f2),
                            "Floor two is meant to be a copy of floor one, batteries aside.");
        }

        private static int CountFurniture(Transform floor)
        {
            return floor.GetComponentsInChildren<Transform>(true)
                        .Count(t => !IsUnder(t, "Batteries"));
        }

        [Test]
        public void EveryInteriorLightIsInsideTheBuilding()
        {
            Light[] lights = Root.GetComponentsInChildren<Light>(includeInactive: true)
                                 .Where(l => l.type != LightType.Directional)
                                 .Where(l => !IsUnder(l.transform, "Exterior"))
                                 .ToArray();

            Assert.IsNotEmpty(lights, "The building has no interior lights at all.");

            string[] strays = lights
                .Where(l =>
                {
                    Vector3 p = l.transform.position;
                    return p.x <= XWest || p.x >= XEast || p.z <= ZSouth || p.z >= ZNorth ||
                           p.y <= 0f || p.y >= TopOfBuilding;
                })
                .Select(l => $"{l.name} at {l.transform.position}")
                .ToArray();

            Assert.IsEmpty(strays, "Interior lights ended up outside the building: " + string.Join(", ", strays));
        }

        [Test]
        public void MostCeilingFittingsAreDead()
        {
            Transform[] fittings = Root.GetComponentsInChildren<Transform>(includeInactive: true)
                                       .Where(t => t.name.StartsWith("Fixture_"))
                                       .ToArray();

            Assert.Greater(fittings.Length, 25, "Expected fittings throughout both floors.");

            int lit = fittings.Count(f => f.GetComponentInChildren<Light>(includeInactive: true) != null);
            float litShare = lit / (float)fittings.Length;

            // Most of the building is dead, but not uniformly: the working fittings are placed by
            // hand at CecadecLayout.LightPools, because uniform darkness is monotonous rather than
            // frightening — the torch needs pools of light for the black between them to mean
            // anything. This bound is a ceiling on that, not the old "almost nothing is lit" rule.
            // Raised from 0.45 when the pools went from six to ten per floor. The design moved
            // from "a few bright pools" to "many faint ones": the ceiling here is on *count*,
            // and LitFittingsAreWarmAndDim is what stops them getting bright. Both bounds have
            // to be read together, or one of them looks like it was simply relaxed.
            Assert.Less(litShare, 0.75f,
                $"{lit}/{fittings.Length} fittings are lit. The torch is still meant to be the " +
                "real light source; the pools are punctuation, not illumination.");
        }

        [Test]
        public void LitFittingsAreWarmAndDim()
        {
            foreach (Light light in Root.GetComponentsInChildren<Light>(includeInactive: true)
                                        .Where(l => l.type == LightType.Point)
                                        .Where(l => !IsUnder(l.transform, "Exterior"))
                                        .Where(l => !IsUnder(l.transform, "CDS")))
            {
                // A pool has to reach the floor and the far wall or it does not read as a pool,
                // which is why this is 2.5 and not the 1.0 it was when every fitting was a
                // uniform, decorative glow.
                Assert.LessOrEqual(light.intensity, 1.5f,
                    $"'{light.name}' is brighter than a failing tube should ever be.");
                Assert.Greater(light.color.r, light.color.b,
                    $"'{light.name}' is not a warm colour.");
            }
        }

        // ---------------------------------------------------------------- doors

        [Test]
        public void EveryDoorHasItsLeavesWired()
        {
            // Cecadec's own doors only: CDS is a separate building with its own two, and
            // counting them here would make this test fail every time that building changes.
            Door[] doors = Root.GetComponentsInChildren<Door>(includeInactive: true)
                               .Where(d => !IsUnder(d.transform, "CDS"))
                               .ToArray();

            // Ground floor: 2 glass entrances + 5 interior. Upper floor: 5 interior, no entrances.
            Assert.AreEqual(12, doors.Length, "Unexpected door count across both floors.");

            foreach (Door door in doors)
            {
                var so = new SerializedObject(door);
                SerializedProperty leaves = so.FindProperty("leaves");
                Assert.Greater(leaves.arraySize, 0, $"Door '{door.name}' has no leaves; it cannot open.");

                for (int i = 0; i < leaves.arraySize; i++)
                    Assert.IsNotNull(leaves.GetArrayElementAtIndex(i).objectReferenceValue,
                                     $"Door '{door.name}' leaf {i} is unassigned.");
            }
        }

        [Test]
        public void ServiciosEscolaresIsLockedOnEveryFloor()
        {
            Door[] blocked = Root.GetComponentsInChildren<Door>(includeInactive: true)
                                 .Where(d => d.name.Contains("Servicios"))
                                 .ToArray();

            Assert.AreEqual(CecadecLevelBuilder.FloorCount, blocked.Length);
            foreach (Door door in blocked)
                Assert.IsTrue(door.IsLocked, "Servicios Escolares is a no-access room on the plan.");
        }

        [Test]
        public void EveryDoorwayIsWalkThrough()
        {
            // Ignore the leaves: a shut door is meant to block. What must never block is the
            // structure around the hole. The opening wall pieces used to be one mesh with a
            // hole, and the box collider taken from their bounds walled every doorway back up,
            // which made the building impossible to enter.
            int structureOnly = ~(1 << GameLayers.Interactable);

            foreach (Door door in Root.GetComponentsInChildren<Door>(includeInactive: true))
            {
                Vector3 foot = door.transform.position;
                Assert.IsFalse(
                    Physics.CheckCapsule(foot + Vector3.up * 0.45f, foot + Vector3.up * 1.70f,
                                         0.35f, structureOnly),
                    $"The doorway at '{door.name}' is blocked by structure; the door cannot be walked through.");
            }
        }

        [Test]
        public void EveryDoorCanBeAimedAt()
        {
            // Mirrors PlayerInteractor: same mask, same range, same "walk up to the parent"
            // resolution. Door leaves once sat outside their own opening, so the ray found
            // nothing and pressing Interact did nothing at all.
            foreach (Door door in Root.GetComponentsInChildren<Door>(includeInactive: true))
            {
                Vector3 eye = door.transform.position + Vector3.up * 1.62f;
                bool reachable = false;

                foreach (Vector3 approach in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    RaycastHit[] hits = Physics.RaycastAll(eye - approach * 1.5f, approach, 2.5f,
                                                           GameLayers.InteractionMask,
                                                           QueryTriggerInteraction.Collide);
                    if (hits.Any(h => h.collider.GetComponentInParent<Door>() == door))
                    {
                        reachable = true;
                        break;
                    }
                }

                Assert.IsTrue(reachable, $"'{door.name}' cannot be hit by the interaction ray from any side.");
            }
        }

        // ---------------------------------------------------------------- stairs

        [Test]
        public void TheStairsReachTheUpperFloor()
        {
            StairBuilder.Tread top = StairBuilder
                .WalkPoints(StairCentre, StairOuterSize, StairRunWidth, FloorToFloor, 0f)
                .Last();

            Assert.AreEqual(FloorToFloor, top.Position.y, 0.01f,
                "The top landing must finish level with floor two's walking surface.");
        }

        [Test]
        public void EveryStepIsStandableWithHeadroom()
        {
            const float minHeadroom = 2.0f;

            foreach (StairBuilder.Tread tread in
                     StairBuilder.WalkPoints(StairCentre, StairOuterSize, StairRunWidth, FloorToFloor, 0f))
            {
                Vector3 foot = tread.Position;

                Assert.IsTrue(
                    Physics.Raycast(foot + Vector3.up * 0.4f, Vector3.down, out RaycastHit down, 1.2f),
                    $"Nothing to stand on at {foot}.");
                Assert.Less(down.distance, 0.6f, $"Step at {foot} is a {down.distance:0.00} m drop.");

                // The real hazard on a spiral is the flight overhead. A capsule test would be
                // wrong here: with 0.38 m treads a player-sized capsule always clips the next
                // step, which is true of every staircase ever built.
                if (Physics.Raycast(foot + Vector3.up * 0.05f, Vector3.up, out RaycastHit up, minHeadroom))
                    Assert.Fail($"Only {up.distance:0.00} m of headroom above the step at {foot} " +
                                $"(hit '{up.collider.name}'); {minHeadroom} m needed.");
            }
        }

        [Test]
        public void EveryLandingFitsAPlayer()
        {
            const float radius = 0.34f;   // the player's CharacterController radius, near enough

            foreach (StairBuilder.Tread landing in
                     StairBuilder.WalkPoints(StairCentre, StairOuterSize, StairRunWidth, FloorToFloor, 0f)
                                 .Where(t => t.IsLanding))
            {
                Vector3 foot = landing.Position;
                Assert.IsFalse(
                    Physics.CheckCapsule(foot + Vector3.up * (radius + 0.05f),
                                         foot + Vector3.up * 1.75f, radius),
                    $"A player standing on the landing at {foot} would be inside geometry.");
            }
        }

        [Test]
        public void TheTopLandingOpensOntoTheUpperCorridor()
        {
            StairBuilder.Tread top = StairBuilder
                .WalkPoints(StairCentre, StairOuterSize, StairRunWidth, FloorToFloor, 0f)
                .Last();

            // The parapet gap sits south of the top landing, mirroring the entry below.
            Vector3 exit = top.Position + new Vector3(0f, 0f, -1.6f);

            Assert.IsTrue(Physics.Raycast(exit + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f),
                          "No floor where the stairs come out on floor two.");
            Assert.Less(hit.distance, 0.8f, "Stepping off the top landing would be a drop.");
            Assert.IsFalse(Physics.CheckCapsule(exit + Vector3.up * 0.4f, exit + Vector3.up * 1.75f, 0.34f),
                           "The way off the top landing is blocked.");
        }

        // ---------------------------------------------------------------- spawn

        [Test]
        public void PlayerSpawnsOutsideOnSolidGround()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "No Player in the scene.");

            Vector3 p = player.transform.position;
            Assert.Greater(p.z, ZNorth, "The player should start outside, in front of the main entrance.");
            Assert.IsTrue(Physics.Raycast(p + Vector3.up, Vector3.down, out RaycastHit hit, 5f),
                          "Nothing under the spawn point — the player would fall forever.");
            Assert.Less(hit.distance, 2f, $"Spawn is {hit.distance:0.0} m above the ground.");
        }

        private static bool IsUnder(Transform t, string ancestorName)
        {
            for (Transform c = t; c != null; c = c.parent)
                if (c.name == ancestorName) return true;
            return false;
        }
    }
}
