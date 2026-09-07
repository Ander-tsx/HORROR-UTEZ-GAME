using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UtezHorror.Environment;
using UtezHorror.Interaction;
using UtezHorror.Utils;
using Random = System.Random;
using static UtezHorror.EditorTools.Level.CecadecLayout;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Builds Cecadec natively in Unity from <see cref="CecadecLayout"/>.
    ///
    /// Deliberately not an imported FBX. The previous build went through Blender and every
    /// world position had to survive a join pivot, an axis flip and a scale redistribution —
    /// which is why the light fixtures ended up on the lawn. Here the walls, the rooms and the
    /// fixtures all read the same constants, so a fixture cannot land where the room is not.
    ///
    /// Each floor is built into its own transform, offset in Y. Every factory places geometry
    /// in local space, so floor two is the same code with a different parent.
    /// </summary>
    public static class CecadecLevelBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Cecadec.unity";
        public const int FloorCount = 2;

        /// <summary>Fixed so rebuilding does not reshuffle which tubes are dead.</summary>
        private const int LightSeed = 20260905;

        /// <summary>
        /// How a ceiling fixture behaves. The overwhelming majority are <see cref="Dead"/> and
        /// carry no Light component at all: the torch is the real light source, and these exist
        /// to make the darkness feel like a building that stopped being maintained.
        /// </summary>
        private enum FixtureMode { Dead, Failing, Weak }

        /// <summary>Metres between ceiling fittings. Sparse on purpose: fewer, deader fittings.</summary>
        private const float FixtureSpacing = 10f;

        /// <summary>
        /// Metres per texture repeat, matching the density the placeholder textures are
        /// authored at. Every surface uses it, which is what keeps texel density uniform.
        /// </summary>
        private const float Tiling = LevelTextures.MetresPerRepeat;

        private static Material Wall => LevelPrimitives.Mat("WallPaint", new Color(0.82f, 0.86f, 0.78f), 0.05f,
                                                            baseMap: LevelTextures.Wall);
        private static Material FloorTile => LevelPrimitives.Mat("FloorTile", new Color(0.76f, 0.80f, 0.74f), 0.15f,
                                                                 baseMap: LevelTextures.FloorTile);
        private static Material CorridorTile => LevelPrimitives.Mat("CorridorTile", new Color(0.80f, 0.84f, 0.78f), 0.15f,
                                                                    baseMap: LevelTextures.FloorTile);
        private static Material Ceiling => LevelPrimitives.Mat("Ceiling", new Color(0.78f, 0.82f, 0.74f), 0.03f,
                                                               baseMap: LevelTextures.Ceiling);
        private static Material Pavement => LevelPrimitives.Mat("Pavement", new Color(0.21f, 0.23f, 0.20f), 0.05f);

        private static Material FixtureBody =>
            LevelPrimitives.Mat("FixtureBody", new Color(0.24f, 0.26f, 0.23f), 0.12f);

        /// <summary>
        /// A dead tube: plain grey with no emission, so it is invisible in the dark until the
        /// torch lands on it. An emissive fitting reads as "on" no matter how dim it is, which
        /// is what made the first pass glow white across the whole building.
        /// </summary>
        private static Material FixtureTubeDead =>
            LevelPrimitives.Mat("FixtureTubeDead", new Color(0.38f, 0.40f, 0.36f), 0.10f);

        private static Material FixtureTubeLit =>
            LevelPrimitives.Mat("FixtureTubeLit", new Color(0.72f, 0.68f, 0.60f), 0.3f,
                                emission: new Color(1f, 0.86f, 0.66f) * 0.8f);

        /// <summary>Sodium-warm and weak. Enough to see the fitting, not enough to navigate by.</summary>
        private static readonly Color TubeColour = new(1f, 0.63f, 0.30f);

        [MenuItem("Tools/UtezHorror/Build Cecadec Level")]
        public static void Build()
        {
            LevelPrimitives.ResetCache();
            KitLibrary.ResetCache();
            LevelPrimitives.EnsureFolder("Assets/_Project/Art/Materials/Level");
            BoxMeshLibrary.Begin();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneLightingSetup.ApplyInterior();

            var level = new GameObject("Cecadec") { isStatic = true };
            Transform root = level.transform;

            BuildExterior(root);

            var rng = new Random(LightSeed);
            for (int floor = 0; floor < FloorCount; floor++)
            {
                var floorRoot = new GameObject($"Floor{floor + 1}") { isStatic = true };
                floorRoot.transform.SetParent(root, false);
                floorRoot.transform.localPosition = new Vector3(0f, floor * FloorToFloor, 0f);
                BuildFloor(floorRoot.transform, floor, rng);
            }

            BuildStairwell(root, rng);

            PlayerRigBuilder.Build(PlayerSpawn);
            PlayerRigBuilder.BuildGameManager();

            // Order matters: bake first, then spawn. Baking with the enemy already present
            // carves its own collider out of the mesh and leaves it standing in a hole.
            NavMeshBaker.Bake(level);
            EnemyFactory.Build(root);

            VerifyLightsAreIndoors();

            BoxMeshLibrary.End();
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene();
            AssetDatabase.SaveAssets();

            Transform[] fittings = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                                                     .Where(t => t.name.StartsWith("Fixture_")).ToArray();
            int lit = fittings.Count(f => f.GetComponentInChildren<Light>(true) != null);
            int total = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;

            Debug.Log($"[Cecadec] Built {FloorCount} floors, corridor {MainCorridorWidth:0.0} m wide. " +
                      $"{fittings.Length} ceiling fittings, {lit} of them lit " +
                      $"({total - lit} other lights: moon and lamp posts).");
        }

        // ------------------------------------------------------------------ per floor

        private static void BuildFloor(Transform parent, int floor, Random rng)
        {
            bool isGround = floor == 0;

            // Floor two stands on floor one's ceiling slab, so only the ground floor needs one.
            if (isGround) BuildFloorSlab(parent);

            var doors = LevelPrimitives.Group(parent, "Doors");
            BuildExteriorWalls(parent, doors.transform, withEntrances: isGround);
            BuildInteriorWalls(parent, doors.transform);

            // The stairwell pierces the ground floor's ceiling; the top floor's ceiling is the roof.
            BuildCeiling(parent, withStairHole: isGround);
            BuildRooms(parent, floor);
            BuildFixtures(parent, rng);
        }

        private static void BuildFloorSlab(Transform parent)
        {
            var group = LevelPrimitives.Group(parent, "FloorSlab");

            foreach ((Rect rect, string name, bool corridor) in AllRects())
                LevelPrimitives.Slab(group.transform, $"Floor_{name}", rect.min, rect.max,
                                     -SlabThickness, SlabThickness,
                                     corridor ? CorridorTile : FloorTile, GameLayers.Environment, Tiling);
        }

        private static void BuildExteriorWalls(Transform parent, Transform doors, bool withEntrances)
        {
            var group = LevelPrimitives.Group(parent, "ExteriorWalls");

            KitOpening[] north = withEntrances
                ? new[] { new KitOpening(XMainEntrance - XWest, KitOpeningKind.Entrance) }
                : System.Array.Empty<KitOpening>();
            KitOpening[] east = withEntrances
                ? new[] { new KitOpening(ZSideEntrance - ZSouth, KitOpeningKind.Entrance) }
                : System.Array.Empty<KitOpening>();

            KitPlacer.Wall(group.transform, "North", new Vector2(XWest, ZNorth), new Vector2(XEast, ZNorth),
                           exterior: true, GameLayers.Environment, north);
            KitPlacer.Wall(group.transform, "South", new Vector2(XWest, ZSouth), new Vector2(XEast, ZSouth),
                           exterior: true, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "West", new Vector2(XWest, ZSouth), new Vector2(XWest, ZNorth),
                           exterior: true, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "East", new Vector2(XEast, ZSouth), new Vector2(XEast, ZNorth),
                           exterior: true, GameLayers.Environment, east);

            if (!withEntrances) return;

            DoorFactory.Build(doors, "Entrance_North", new Vector3(XMainEntrance, 0f, ZNorth), Vector2.right,
                              doubleLeaf: true, glass: true, locked: false);
            DoorFactory.Build(doors, "Entrance_East", new Vector3(XEast, 0f, ZSideEntrance), Vector2.up,
                              doubleLeaf: true, glass: true, locked: false);
        }

        private static void BuildInteriorWalls(Transform parent, Transform doors)
        {
            var group = LevelPrimitives.Group(parent, "InteriorWalls");

            KitPlacer.Wall(group.transform, "W_NorthBlock",
                           new Vector2(XWestBlock, ZCrossNorth), new Vector2(XWestBlock, ZNorth),
                           exterior: false, GameLayers.Environment,
                           new[] { new KitOpening(ZDoorServiciosBlocked - ZCrossNorth, KitOpeningKind.GlassDoor) });

            KitPlacer.Wall(group.transform, "E_NorthBlock",
                           new Vector2(XEastBlock, ZCrossNorth), new Vector2(XEastBlock, ZNorth),
                           exterior: false, GameLayers.Environment,
                           new[]
                           {
                               new KitOpening(ZDoorCentroComputo - ZCrossNorth, KitOpeningKind.Door),
                               new KitOpening(ZDoorAulaNormal - ZCrossNorth, KitOpeningKind.Door)
                           });

            KitPlacer.Wall(group.transform, "E_RoomSplit",
                           new Vector2(XEastBlock, ZRoomSplit), new Vector2(XEast, ZRoomSplit),
                           exterior: false, GameLayers.Environment);

            KitPlacer.Wall(group.transform, "CrossNorth_West",
                           new Vector2(XWest, ZCrossNorth), new Vector2(XWestBlock, ZCrossNorth),
                           exterior: false, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "CrossNorth_East",
                           new Vector2(XEastBlock, ZCrossNorth), new Vector2(XEast, ZCrossNorth),
                           exterior: false, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "CrossSouth_West",
                           new Vector2(XWest, ZSouthRooms), new Vector2(XWestBlock, ZSouthRooms),
                           exterior: false, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "CrossSouth_East",
                           new Vector2(XEastBlock, ZSouthRooms), new Vector2(XEast, ZSouthRooms),
                           exterior: false, GameLayers.Environment);

            KitPlacer.Wall(group.transform, "W_SouthBlock",
                           new Vector2(XWestBlock, ZSouth), new Vector2(XWestBlock, ZSouthRooms),
                           exterior: false, GameLayers.Environment,
                           new[] { new KitOpening(ZDoorAulaComputoWest - ZSouth, KitOpeningKind.Door) });
            KitPlacer.Wall(group.transform, "E_SouthBlock",
                           new Vector2(XEastBlock, ZSouth), new Vector2(XEastBlock, ZSouthRooms),
                           exterior: false, GameLayers.Environment,
                           new[] { new KitOpening(ZDoorAulaComputoEast - ZSouth, KitOpeningKind.Door) });

            DoorFactory.Build(doors, "Door_ServiciosEscolares_Blocked",
                              new Vector3(XWestBlock, 0f, ZDoorServiciosBlocked), Vector2.up,
                              doubleLeaf: true, glass: true, locked: true);
            DoorFactory.Build(doors, "Door_AulaNormal",
                              new Vector3(XEastBlock, 0f, ZDoorAulaNormal), Vector2.up,
                              doubleLeaf: false, glass: false, locked: false);
            DoorFactory.Build(doors, "Door_CentroComputo",
                              new Vector3(XEastBlock, 0f, ZDoorCentroComputo), Vector2.up,
                              doubleLeaf: false, glass: false, locked: false);
            DoorFactory.Build(doors, "Door_AulaComputoOeste",
                              new Vector3(XWestBlock, 0f, ZDoorAulaComputoWest), Vector2.up,
                              doubleLeaf: false, glass: false, locked: false);
            DoorFactory.Build(doors, "Door_AulaComputoEste",
                              new Vector3(XEastBlock, 0f, ZDoorAulaComputoEast), Vector2.up,
                              doubleLeaf: false, glass: false, locked: false);
        }

        private static void BuildCeiling(Transform parent, bool withStairHole)
        {
            var group = LevelPrimitives.Group(parent, "Ceiling");

            foreach ((Rect rect, string name, bool _) in AllRects())
            {
                bool pierced = withStairHole && name == "CorridorCross";
                if (pierced)
                    SlabWithHole(group.transform, $"Ceil_{name}", rect, StairFootprint,
                                 CeilingHeight, SlabThickness, Ceiling);
                else
                    LevelPrimitives.Slab(group.transform, $"Ceil_{name}", rect.min, rect.max,
                                         CeilingHeight, SlabThickness, Ceiling, GameLayers.Environment, Tiling);
            }
        }

        /// <summary>Slab split into up to four bands so a rectangular hole is left open.</summary>
        private static void SlabWithHole(Transform parent, string name, Rect rect, Rect hole,
                                         float y, float thickness, Material material)
        {
            if (rect.yMin < hole.yMin)
                LevelPrimitives.Slab(parent, name + "_S", rect.min, new Vector2(rect.xMax, hole.yMin),
                                     y, thickness, material, GameLayers.Environment, Tiling);
            if (hole.yMax < rect.yMax)
                LevelPrimitives.Slab(parent, name + "_N", new Vector2(rect.xMin, hole.yMax), rect.max,
                                     y, thickness, material, GameLayers.Environment, Tiling);
            if (rect.xMin < hole.xMin)
                LevelPrimitives.Slab(parent, name + "_W", new Vector2(rect.xMin, hole.yMin),
                                     new Vector2(hole.xMin, hole.yMax), y, thickness, material,
                                     GameLayers.Environment, Tiling);
            if (hole.xMax < rect.xMax)
                LevelPrimitives.Slab(parent, name + "_E", new Vector2(hole.xMax, hole.yMin),
                                     new Vector2(rect.xMax, hole.yMax), y, thickness, material,
                                     GameLayers.Environment, Tiling);
        }

        // ------------------------------------------------------------------ rooms

        private static void BuildRooms(Transform parent, int floor)
        {
            var group = LevelPrimitives.Group(parent, "Furniture");
            int props = GameLayers.Prop;

            var aula = LevelPrimitives.Group(group.transform, "AulaNormal").transform;
            foreach (float x in new[] { 23f, 25f, 27f, 29f, 31f })
                foreach (float z in Series(49f, 1.6f, 5))
                    KitPlacer.Prop(aula, "Butaca", new Vector3(x, 0f, z), 0f, props);

            KitPlacer.Prop(aula, "Teacher_Desk", new Vector3(27f, 0f, 58.5f), 180f, props);
            KitPlacer.Prop(aula, "Chair_Office", new Vector3(27f, 0f, 59.2f), 0f, props);
            KitPlacer.Prop(aula, "Board_Wall", new Vector3(27f, 0f, ZNorth - 0.25f), 180f, props, collider: false);

            // The bench you build your own machine on: the run's ending, in a fixed place the
            // player walks past early. Docs/Plans/01 argues for a climax rather than a counter
            // reaching 4/4, and a known location is what makes the last twenty seconds tense.
            GameObject bench = KitPlacer.Prop(aula, "Teacher_Desk", new Vector3(23f, 0f, 50f), 90f,
                                              GameLayers.Interactable, name: "AssemblyBench");
            if (bench != null)
            {
                bench.AddComponent<AssemblyBench>();
                bench.isStatic = false;
            }

            // The floor is part of the room's identity. Floor two is a copy of floor one, so
            // without it "Centro de cómputo" names two different rooms and the objective
            // distribution happily puts a component in each, thinking they are one place.
            ComputerRoom(group.transform, "CentroComputo", floor,
                         new[] { 23.5f, 26.3f, 29.1f, 31.9f }, Series(29f, 2.5f, 7));
            ComputerRoom(group.transform, "AulaComputoOeste", floor,
                         new[] { 2.2f, 5.6f, 9.0f }, Series(2.5f, 2.5f, 4));
            ComputerRoom(group.transform, "AulaComputoEste", floor,
                         new[] { 23.5f, 26.9f, 30.3f }, Series(2.5f, 2.5f, 4));

            CorridorBenches(group.transform);
            Batteries(group.transform, floor);

            var servicios = LevelPrimitives.Group(group.transform, "ServiciosEscolares").transform;
            foreach (float x in new[] { 3.0f, 8.4f })
                foreach (float z in Series(26.5f, 7.0f, 5))
                {
                    KitPlacer.Prop(servicios, "Cubicle", new Vector3(x, 0f, z), 0f, props);
                    KitPlacer.Prop(servicios, "Chair_Office", new Vector3(x, 0f, z - 0.1f), 0f, props);
                }
        }

        /// <summary>
        /// Waiting benches down the corridors, backs to the walls.
        ///
        /// They earn their place three times over: the corridor is 9 m wide and reads as a
        /// tunnel without anything breaking its length; the torch needs solid objects to catch
        /// so its beam looks like a cone instead of a moving stain; and they are the first thing
        /// in the building that is recognisably UTEZ rather than generic architecture.
        ///
        /// <c>Bench_Metal</c> is derived from a downloaded model by Tools/Blender/make_bench.py:
        /// 118,400 triangles down to 878, flat shaded, scaled to a real 1.80 m.
        /// </summary>
        private static void CorridorBenches(Transform parent)
        {
            Transform group = LevelPrimitives.Group(parent, "CorridorBenches").transform;
            int props = GameLayers.Prop;

            // The piece runs its length along local +X. Its back is on -Y once the .blend has
            // been through the importer's X mirror, not +Y as the Blender file suggests — the
            // first pass assumed the file and every bench ended up facing the wall it was
            // supposed to have its back to. Same mirror that caught the door leaves.
            const float westLane = 12.6f;   // clear of the west wall plus half the bench depth
            const float eastLane = 20.4f;

            foreach (float z in new[] { 30f, 36f, 42f })
                KitPlacer.Prop(group, "Bench_Metal", new Vector3(westLane, 0f, z), 270f, props,
                               name: $"Bench_West_{z:0}");

            // East side skips the door mouths at the centro de cómputo (z 26) and the aula
            // normal (z 49): a bench across a doorway is the classic blockout mistake.
            foreach (float z in new[] { 33f, 39f, 54f })
                KitPlacer.Prop(group, "Bench_Metal", new Vector3(eastLane, 0f, z), 90f, props,
                               name: $"Bench_East_{z:0}");

            // Cross corridor, backs to its south wall.
            foreach (float x in new[] { 7f, 27f })
                KitPlacer.Prop(group, "Bench_Metal", new Vector3(x, 0f, 14.6f), 180f, props,
                               name: $"Bench_Cross_{x:0}");
        }

        /// <summary>
        /// Spare batteries for the phone torch, left where someone might plausibly have put one
        /// down: on a seat, on a desk beside a machine.
        ///
        /// Five in the entire building, and that is the design, not a placeholder. The torch is
        /// the run's only consumable and running out is what forces the player to keep moving
        /// through corridors they would rather avoid. Make batteries common and that pressure
        /// disappears, and with it the reason the darkness matters at all. Each one is worth
        /// about eighty seconds of light against a four-minute full charge.
        ///
        /// Split across both floors on purpose: if they were all downstairs there would be no
        /// reason to ever take the stair, and the upper floor would be scenery.
        ///
        /// Positions are all on the centre line of a piece of furniture. Kit pieces come through
        /// the importer with X mirrored, so an offset sideways onto, say, the writing paddle of
        /// a butaca can land on the wrong side and leave the battery floating in mid-air. Offset
        /// in depth if you need to; never sideways.
        /// </summary>
        private static void Batteries(Transform parent, int floor)
        {
            Transform group = LevelPrimitives.Group(parent, "Batteries").transform;

            // Pale against a building of dirty greens and greys, so the torch picks it out
            // immediately. No emission: an object that glows in the dark would find itself, and
            // finding things is the torch's job.
            Material material = LevelPrimitives.Mat("Battery", new Color(0.86f, 0.84f, 0.52f), 0.30f,
                                                    baseMap: LevelTextures.Metal,
                                                    triplanar: true, triplanarScale: 0.35f);

            (Vector3 position, string where)[] spots = floor == 0
                ? new[]
                {
                    (new Vector3(25f, 0.50f, 52.2f), "AulaNormal_Butaca"),      // on a seat
                    (new Vector3(29.1f, 0.79f, 36.7f), "CentroComputo_Desk"),
                    (new Vector3(5.6f, 0.79f, 7.7f), "AulaComputoOeste_Desk")
                }
                : new[]
                {
                    (new Vector3(27f, 0.82f, 58.3f), "AulaNormal_TeacherDesk"),
                    (new Vector3(23.5f, 0.79f, 41.7f), "CentroComputo_Desk")
                };

            foreach ((Vector3 position, string where) in spots)
            {
                GameObject box = LevelPrimitives.Box(group, $"Battery_{where}", position,
                                                     new Vector3(0.09f, 0.035f, 0.16f),
                                                     material, GameLayers.Interactable,
                                                     collider: true, tiling: 0.35f);
                box.isStatic = false;   // it is removed from the world when taken
                box.AddComponent<BatteryPickup>();
            }
        }

        private static void ComputerRoom(Transform parent, string name, int floor,
                                         float[] columns, float[] rows)
        {
            Transform room = LevelPrimitives.Group(parent, name).transform;
            string roomId = $"{name}_F{floor + 1}";
            foreach (float x in columns)
                foreach (float z in rows)
                {
                    GameObject desk = KitPlacer.Prop(room, "Desk_PC", new Vector3(x, 0f, z), 0f,
                                                     GameLayers.Prop);
                    KitPlacer.Prop(room, "Chair_Office", new Vector3(x, 0f, z + 0.62f), 180f, GameLayers.Prop);

                    // Every machine is a candidate; ObjectiveSystem loads a handful at the start
                    // of each run. Placing the components here instead would make the building
                    // memorisable in two attempts.
                    if (desk == null) continue;
                    var theft = desk.AddComponent<TheftTarget>();
                    theft.SetRoom(roomId);
                    desk.isStatic = false;   // it changes state during a run
                }
        }

        private static float[] Series(float start, float step, int count) =>
            Enumerable.Range(0, count).Select(i => start + step * i).ToArray();

        // ------------------------------------------------------------------ fixtures

        private static void BuildFixtures(Transform parent, Random rng)
        {
            var group = LevelPrimitives.Group(parent, "Fixtures");
            int index = 0;

            foreach ((Rect rect, string name, bool _) in AllRects())
                foreach (Vector2 p in GridInside(rect, spacing: FixtureSpacing, inset: 3f))
                {
                    if (StairFootprint.Contains(p)) continue;   // the shaft has its own fittings
                    CeilingFixture(group.transform, $"Fixture_{name}_{index++}", p, PickMode(p, rng));
                }
        }

        /// <summary>
        /// Which fittings work, decided by the hand-placed pools in
        /// <see cref="CecadecLayout.LightPools"/> rather than by a dice roll.
        ///
        /// The previous version dealt modes out at random with an 82% dead share. That gives
        /// *uniform* darkness, and uniform darkness is monotonous rather than frightening — the
        /// torch reveals nothing because there is nothing to reveal it against. Placing the light
        /// deliberately gives the building a rhythm: pools you navigate by, a flickering fringe
        /// around them, and real black in between that now means something.
        /// </summary>
        private static FixtureMode PickMode(Vector2 position, Random rng)
        {
            float distance = DistanceToNearestPool(position);

            if (distance <= LightPoolRadius) return FixtureMode.Weak;

            // The fringe is where a pool frays into the dark. Failing fittings here do the most
            // work: a light that dies as you approach it is worse than one that was never on.
            if (distance <= LightPoolFringe)
                return rng.NextDouble() < 0.22 ? FixtureMode.Failing : FixtureMode.Dead;

            return FixtureMode.Dead;
        }

        private static float DistanceToNearestPool(Vector2 position)
        {
            float best = float.MaxValue;
            foreach (Vector2 pool in LightPools)
                best = Mathf.Min(best, Vector2.Distance(position, pool));
            return best;
        }

        private static void CeilingFixture(Transform parent, string name, Vector2 position, FixtureMode mode)
        {
            GameObject fitting = KitPlacer.Prop(parent, "Light_Fixture",
                                                new Vector3(position.x, CeilingHeight, position.y),
                                                0f, GameLayers.Prop, collider: false, name: name);
            if (fitting == null) return;

            Renderer tube = FindChildRenderer(fitting.transform, "Tube");

            // A dead fitting is set dressing only: the tube is swapped for the unlit material and
            // there is no Light or FlickeringLight, so it costs nothing per frame.
            if (mode == FixtureMode.Dead)
            {
                if (tube != null) tube.sharedMaterial = FixtureTubeDead;
                return;
            }

            if (tube != null)
            {
                tube.sharedMaterial = FixtureTubeLit;
                tube.gameObject.isStatic = false;   // toggled at runtime, so it cannot be batched
            }

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(fitting.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.2f, 0f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = TubeColour;

            // More fittings, each dimmer. The brief is "faint but working": enough that a room
            // has a shape you can navigate and remember, never enough to replace the torch.
            // Raising the count and lowering the intensity is what buys both — a handful of
            // bright pools left every room between them identical and pitch black.
            bool weak = mode == FixtureMode.Weak;
            light.range = weak ? 11f : 6.5f;
            light.intensity = weak ? 1.15f : 0.45f;

            // Only the handful of steady fittings pay for shadows; the failing ones are out
            // most of the time and would spend shadow atlas on nothing. Hard, not soft: soft
            // shadow filtering did not exist in 1997 and costs more.
            light.shadows = weak ? LightShadows.Hard : LightShadows.None;
            light.shadowStrength = 0.9f;

            var flicker = lightGo.AddComponent<FlickeringLight>();
            ConfigureFlicker(flicker, tube, mode);
        }

        private static Renderer FindChildRenderer(Transform root, string childName)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name == childName) return r;
            return null;
        }

        private static void ConfigureFlicker(FlickeringLight flicker, Renderer glow, FixtureMode mode)
        {
            var so = new SerializedObject(flicker);
            so.FindProperty("glow").objectReferenceValue = glow;

            if (mode == FixtureMode.Weak)
            {
                // Holds for a long while, stutters, occasionally dies. The closest thing to
                // a working light in the building.
                so.FindProperty("stableDuration").vector2Value = new Vector2(20f, 60f);
                so.FindProperty("flickerDuration").vector2Value = new Vector2(0.3f, 1.6f);
                so.FindProperty("outDuration").vector2Value = new Vector2(6f, 22f);
                so.FindProperty("outChance").floatValue = 0.35f;
                so.FindProperty("startDeadChance").floatValue = 0.15f;
            }
            else
            {
                // Out most of the time; comes back for a second or two and gives up again.
                so.FindProperty("stableDuration").vector2Value = new Vector2(0.3f, 1.5f);
                so.FindProperty("flickerDuration").vector2Value = new Vector2(0.25f, 1.4f);
                so.FindProperty("outDuration").vector2Value = new Vector2(14f, 55f);
                so.FindProperty("outChance").floatValue = 0.88f;
                so.FindProperty("startDeadChance").floatValue = 0.85f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ stairwell

        private static Rect StairFootprint =>
            Rect.MinMaxRect(StairCentre.x - StairOuterSize * 0.5f, StairCentre.y - StairOuterSize * 0.5f,
                            StairCentre.x + StairOuterSize * 0.5f, StairCentre.y + StairOuterSize * 0.5f);

        /// <summary>Distance from the shaft's south-west corner to the centre of the way in.</summary>
        private static float StairEntryOffset => StairRunWidth * 0.5f + 0.1f;

        private static void BuildStairwell(Transform root, Random rng)
        {
            StairBuilder.SquareSpiral(root, StairCentre, StairOuterSize, StairRunWidth, FloorToFloor, 0f);

            var group = LevelPrimitives.Group(root, "StairShaft");
            Rect f = StairFootprint;

            // North, east and west only. The south face is left open to the corridor: an
            // opening module is centred on the module, and the bottom landing is in the
            // corner, so a doorway there would not line up with where you actually walk in.
            KitPlacer.Wall(group.transform, "Shaft_North",
                           new Vector2(f.xMin, f.yMax), new Vector2(f.xMax, f.yMax),
                           exterior: false, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "Shaft_West",
                           new Vector2(f.xMin, f.yMin), new Vector2(f.xMin, f.yMax),
                           exterior: false, GameLayers.Environment);
            KitPlacer.Wall(group.transform, "Shaft_East",
                           new Vector2(f.xMax, f.yMin), new Vector2(f.xMax, f.yMax),
                           exterior: false, GameLayers.Environment);

            // Parapet on floor two, with a gap over the top landing so the stairs come out
            // onto the corridor instead of into a wall. Kept procedural: it is a low rail on
            // an exact corner, not a module-aligned run.
            var parapet = LevelPrimitives.Group(root, "StairParapet");
            const float t = 0.2f;
            float gapCentre = StairRunWidth * 0.5f;   // the top landing sits in the corner

            LevelPrimitives.Wall(parapet.transform, "Parapet_South",
                                 new Vector2(f.xMin, f.yMin), new Vector2(f.xMax, f.yMin), t, 1.1f, Wall,
                                 GameLayers.Environment,
                                 new[] { new Opening(gapCentre, StairRunWidth, 1.1f) });
            LevelPrimitives.Wall(parapet.transform, "Parapet_North",
                                 new Vector2(f.xMin, f.yMax), new Vector2(f.xMax, f.yMax), t, 1.1f, Wall, GameLayers.Environment);
            LevelPrimitives.Wall(parapet.transform, "Parapet_West",
                                 new Vector2(f.xMin, f.yMin), new Vector2(f.xMin, f.yMax), t, 1.1f, Wall, GameLayers.Environment);
            LevelPrimitives.Wall(parapet.transform, "Parapet_East",
                                 new Vector2(f.xMax, f.yMin), new Vector2(f.xMax, f.yMax), t, 1.1f, Wall, GameLayers.Environment);
            parapet.transform.localPosition = new Vector3(0f, FloorToFloor, 0f);

            // The shaft is excluded from the ceiling grid, so it gets its own fittings — one per
            // quarter turn. Without them the stairs are unusable even with the torch.
            var stairLights = LevelPrimitives.Group(root, "Fixtures_Stairwell");
            for (int i = 0; i < 4; i++)
            {
                float angle = 90f * i + 45f;
                var offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad))
                             * (StairOuterSize * 0.5f - StairRunWidth * 0.5f);

                CeilingFixtureAt(stairLights.transform, $"Fixture_Stairwell_{i}",
                                 new Vector3(StairCentre.x + offset.x,
                                             FloorToFloor * (i / 4f) + 2.35f,
                                             StairCentre.y + offset.y),
                                 // The stairwell is one of the hand-placed light pools: a
                                 // vertical route the player has to be able to find. Alternating
                                 // steady and failing keeps it lit without flattening it out.
                                 i % 2 == 0 ? FixtureMode.Weak : FixtureMode.Failing);
            }
        }

        /// <summary>Fixture at an explicit height, for places with no ceiling grid.</summary>
        private static void CeilingFixtureAt(Transform parent, string name, Vector3 localPosition, FixtureMode mode)
        {
            CeilingFixture(parent, name, new Vector2(localPosition.x, localPosition.z), mode);
            Transform created = parent.Find(name);
            if (created != null) created.localPosition = localPosition;
        }

        // ------------------------------------------------------------------ exterior

        private static void BuildExterior(Transform root)
        {
            var group = LevelPrimitives.Group(root, "Exterior");

            // The flat ground slab that used to be here is gone: the outdoor world is now a
            // height-mapped mesh with the building pads flattened into it. See TerrainBuilder.
            TerrainBuilder.Build(group.transform);

            // Ring, not a full rectangle: a full one would sit under the interior floor at the
            // same height and z-fight with it across the whole footprint.
            const float apron = 4f;
            foreach ((Vector2 min, Vector2 max, string side) in new[]
                     {
                         (new Vector2(XWest - apron, ZNorth), new Vector2(XEast + apron, ZNorth + apron), "N"),
                         (new Vector2(XWest - apron, ZSouth - apron), new Vector2(XEast + apron, ZSouth), "S"),
                         (new Vector2(XWest - apron, ZSouth), new Vector2(XWest, ZNorth), "W"),
                         (new Vector2(XEast, ZSouth), new Vector2(XEast + apron, ZNorth), "E")
                     })
            {
                LevelPrimitives.Slab(group.transform, $"Apron_{side}", min, max, -0.12f, 0.10f,
                                     Pavement, GameLayers.Environment);
            }

            GameObject generator = KitPlacer.Prop(group.transform, "Generator", GeneratorPosition, 180f,
                                                  GameLayers.Interactable, name: "Generator");
            if (generator != null)
            {
                // It has stood in the yard doing nothing since the first build. This is what it
                // was for: the one place in the game you are guaranteed to stand still, outside,
                // making noise, at a location anything hunting you already knows.
                generator.AddComponent<GeneratorRepair>();
                generator.isStatic = false;
            }
            BuildExteriorLights(group.transform);

            FoliageBuilder.Build(group.transform);
            CdsBuilder.Build(group.transform);
        }

        private static void BuildExteriorLights(Transform parent)
        {
            // Dim moon: enough to cross the yard to the generator, never enough to help indoors,
            // which the roof blocks anyway.
            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(parent, false);
            moonGo.transform.localRotation = Quaternion.Euler(38f, 205f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            // Raised from 0.22: the wood between the buildings is far from any lamp post, and at
            // the old value it was not dark, it was absent — nothing to catch the fog on.
            moon.intensity = 0.45f;
            moon.color = new Color(0.60f, 0.70f, 0.98f);
            moon.shadows = LightShadows.Soft;

            Material metal = LevelPrimitives.Mat("FurnitureMetal", new Color(0.27f, 0.29f, 0.27f), 0.22f, 0.5f);

            foreach (Vector3 p in new[]
                     {
                         new Vector3(XMainEntrance - 6f, 0f, ZNorth + 6f),
                         new Vector3(XEast + 6f, 0f, ZSideEntrance),
                         new Vector3(GeneratorPosition.x - 5f, 0f, GeneratorPosition.z - 2f)
                     })
            {
                var pole = LevelPrimitives.Group(parent, "LampPost");
                pole.transform.localPosition = p;
                LevelPrimitives.BoxOnFloor(pole.transform, "Post", Vector3.zero,
                                           new Vector3(0.16f, 4.2f, 0.16f), metal, GameLayers.Environment);

                var lampGo = new GameObject("Light");
                lampGo.transform.SetParent(pole.transform, false);
                lampGo.transform.localPosition = new Vector3(0f, 4.0f, 0f);
                lampGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Spot;
                lamp.range = 22f;
                lamp.spotAngle = 110f;
                lamp.intensity = 8f;
                lamp.color = new Color(1f, 0.78f, 0.5f);
                lamp.shadows = LightShadows.Soft;
                lampGo.AddComponent<FlickeringLight>();
            }
        }

        // ------------------------------------------------------------------ helpers & checks

        private static IEnumerable<Vector2> GridInside(Rect rect, float spacing, float inset)
        {
            float x0 = rect.xMin + inset, x1 = rect.xMax - inset;
            float z0 = rect.yMin + inset, z1 = rect.yMax - inset;
            if (x1 < x0) x0 = x1 = rect.center.x;
            if (z1 < z0) z0 = z1 = rect.center.y;

            int nx = Mathf.Max(1, Mathf.FloorToInt((x1 - x0) / spacing) + 1);
            int nz = Mathf.Max(1, Mathf.FloorToInt((z1 - z0) / spacing) + 1);

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    yield return new Vector2(
                        nx == 1 ? (x0 + x1) * 0.5f : Mathf.Lerp(x0, x1, i / (float)(nx - 1)),
                        nz == 1 ? (z0 + z1) * 0.5f : Mathf.Lerp(z0, z1, j / (float)(nz - 1)));
        }

        /// <summary>Every enclosed area of a floor, flagged as corridor or room.</summary>
        private static IEnumerable<(Rect rect, string name, bool corridor)> AllRects() => new[]
        {
            (ServiciosEscolares, "ServiciosEscolares", false),
            (AulaNormal, "AulaNormal", false),
            (CentroComputo, "CentroComputo", false),
            (AulaComputoOeste, "AulaComputoOeste", false),
            (AulaComputoEste, "AulaComputoEste", false),
            (CorridorNorth, "CorridorNorth", true),
            (CorridorCross, "CorridorCross", true),
            (CorridorSouth, "CorridorSouth", true)
        };

        /// <summary>
        /// The first build of this building shipped with every fitting on the lawn. Fail loudly
        /// rather than let that happen again unnoticed.
        /// </summary>
        private static void VerifyLightsAreIndoors()
        {
            float ceilingOfTopFloor = FloorCount * FloorToFloor + CeilingHeight;
            int strays = 0;

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!light.transform.root.name.StartsWith("Cecadec")) continue;
                if (!light.name.StartsWith("Light") || light.type == LightType.Directional) continue;

                // Lamp posts live under Exterior and CDS has its own fittings; this check is
                // only about Cecadec's interior lights, which is where the bug it guards was.
                if (IsUnder(light.transform, "Exterior") || IsUnder(light.transform, "CDS")) continue;

                Vector3 p = light.transform.position;
                bool inside = p.x > XWest && p.x < XEast && p.z > ZSouth && p.z < ZNorth &&
                              p.y > 0f && p.y < ceilingOfTopFloor;
                if (inside) continue;

                strays++;
                Debug.LogError($"[Cecadec] Interior light '{light.name}' is outside the building at {p}.", light);
            }

            if (strays == 0)
                Debug.Log("[Cecadec] All interior lights verified inside the building footprint.");
        }

        private static bool IsUnder(Transform t, string ancestorName)
        {
            for (Transform c = t; c != null; c = c.parent)
                if (c.name == ancestorName) return true;
            return false;
        }

        private static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
