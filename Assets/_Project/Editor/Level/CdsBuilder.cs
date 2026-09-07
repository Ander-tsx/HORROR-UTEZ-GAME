using System.Collections.Generic;
using UnityEngine;
using UtezHorror.Utils;
using static UtezHorror.EditorTools.Level.ExteriorLayout;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// The second building, CDS — a shell with one room open.
    ///
    /// Scope decision, recorded in Docs/Plans/03: the design calls for objectives split across
    /// both buildings, which needs CDS to open, but detailing a second building doubles the
    /// content work before the game has an event director, a theft system or an enemy. Opening
    /// exactly one classroom next to the entrance resolves that: the crossing through the wood is
    /// real and compulsory, and it costs one room rather than a building.
    ///
    /// Everything else is sealed with locked doors that say so, rather than with invisible walls,
    /// so the player reads it as a building that is closed rather than as a boundary of the map.
    ///
    /// There is no plan of CDS — the team only drew Cecadec's ground floor — so this is built
    /// from a description and is deliberately simple. Do not treat these dimensions as surveyed.
    /// </summary>
    public static class CdsBuilder
    {
        private const float CeilingHeight = CecadecLayout.CeilingHeight;
        private const float SlabThickness = CecadecLayout.SlabThickness;

        public static void Build(Transform root)
        {
            var building = LevelPrimitives.Group(root, "CDS");

            Material wall = LevelPrimitives.Mat("KitWall", new Color(0.82f, 0.86f, 0.78f), 0.05f,
                                                baseMap: LevelTextures.Wall, triplanar: true);
            Material floor = LevelPrimitives.Mat("KitFloor", new Color(0.78f, 0.82f, 0.76f), 0.15f,
                                                 baseMap: LevelTextures.FloorTile, triplanar: true);
            Material ceiling = LevelPrimitives.Mat("KitCeiling", new Color(0.78f, 0.82f, 0.74f), 0.03f,
                                                   baseMap: LevelTextures.Ceiling, triplanar: true);

            var min = new Vector2(CdsXWest, CdsZSouth);
            var max = new Vector2(CdsXEast, CdsZNorth);

            LevelPrimitives.Slab(building.transform, "Floor", min, max, -SlabThickness, SlabThickness,
                                 floor, GameLayers.Environment);

            BuildShell(building.transform);

            // Two storeys of roof so the silhouette in the fog reads as a teaching block rather
            // than a shed. The upper floor is not enterable and has no interior.
            LevelPrimitives.Slab(building.transform, "Roof", min, max,
                                 CeilingHeight * 2f + SlabThickness, SlabThickness,
                                 ceiling, GameLayers.Environment);

            BuildOpenRoom(building.transform, wall, ceiling);
            BuildEntranceLight(building.transform);
        }

        /// <summary>Exterior walls, with the single doorway on the south face.</summary>
        private static void BuildShell(Transform parent)
        {
            var group = LevelPrimitives.Group(parent, "Shell");

            var southOpenings = new List<KitOpening>
            {
                new(CdsEntranceX - CdsXWest, KitOpeningKind.Door)
            };

            KitPlacer.Wall(group.transform, "South", new Vector2(CdsXWest, CdsZSouth),
                           new Vector2(CdsXEast, CdsZSouth), exterior: true,
                           GameLayers.Environment, southOpenings);

            KitPlacer.Wall(group.transform, "North", new Vector2(CdsXWest, CdsZNorth),
                           new Vector2(CdsXEast, CdsZNorth), exterior: true, GameLayers.Environment);

            KitPlacer.Wall(group.transform, "West", new Vector2(CdsXWest, CdsZSouth),
                           new Vector2(CdsXWest, CdsZNorth), exterior: true, GameLayers.Environment);

            KitPlacer.Wall(group.transform, "East", new Vector2(CdsXEast, CdsZSouth),
                           new Vector2(CdsXEast, CdsZNorth), exterior: true, GameLayers.Environment);

            // The upper storey is a second ring of the same walls, raised. Nothing inside it.
            var upper = LevelPrimitives.Group(parent, "ShellUpper");
            upper.transform.localPosition = new Vector3(0f, CecadecLayout.FloorToFloor, 0f);

            KitPlacer.Wall(upper.transform, "South", new Vector2(CdsXWest, CdsZSouth),
                           new Vector2(CdsXEast, CdsZSouth), exterior: true, GameLayers.Environment);
            KitPlacer.Wall(upper.transform, "North", new Vector2(CdsXWest, CdsZNorth),
                           new Vector2(CdsXEast, CdsZNorth), exterior: true, GameLayers.Environment);
            KitPlacer.Wall(upper.transform, "West", new Vector2(CdsXWest, CdsZSouth),
                           new Vector2(CdsXWest, CdsZNorth), exterior: true, GameLayers.Environment);
            KitPlacer.Wall(upper.transform, "East", new Vector2(CdsXEast, CdsZSouth),
                           new Vector2(CdsXEast, CdsZNorth), exterior: true, GameLayers.Environment);

            // The floor slab between storeys doubles as the open room's ceiling.
            LevelPrimitives.Slab(parent, "MidSlab", new Vector2(CdsXWest, CdsZSouth),
                                 new Vector2(CdsXEast, CdsZNorth), CeilingHeight, SlabThickness,
                                 LevelPrimitives.Mat("KitCeiling", new Color(0.78f, 0.82f, 0.74f), 0.03f,
                                                     baseMap: LevelTextures.Ceiling, triplanar: true),
                                 GameLayers.Environment);
        }

        /// <summary>
        /// The one classroom that opens, immediately inside the entrance, plus the locked doors
        /// that make the rest of the building read as closed rather than as absent.
        /// </summary>
        private static void BuildOpenRoom(Transform parent, Material wall, Material ceiling)
        {
            var group = LevelPrimitives.Group(parent, "OpenRoom");
            Rect room = CdsOpenRoom;

            // Partition running east from the entrance side, with a locked door in it: the
            // player can see there is more building and cannot get to it yet.
            KitPlacer.Wall(group.transform, "Partition_East",
                           new Vector2(room.xMax, room.yMin), new Vector2(room.xMax, room.yMax),
                           exterior: false, GameLayers.Environment,
                           new List<KitOpening> { new(6f, KitOpeningKind.Door) });

            KitPlacer.Wall(group.transform, "Partition_North",
                           new Vector2(room.xMin, room.yMax), new Vector2(room.xMax, room.yMax),
                           exterior: false, GameLayers.Environment);

            var doors = LevelPrimitives.Group(group.transform, "Doors");

            DoorFactory.Build(doors.transform, "Door_CDS_Entrance",
                              new Vector3(CdsEntranceX, 0f, CdsZSouth), Vector2.right,
                              doubleLeaf: false, glass: false, locked: false);

            DoorFactory.Build(doors.transform, "Door_CDS_Interior_Locked",
                              new Vector3(room.xMax, 0f, room.yMin + 6f), Vector2.up,
                              doubleLeaf: false, glass: false, locked: true);

            // A computer room, because the objectives that justify the crossing are PC parts.
            var desks = LevelPrimitives.Group(group.transform, "Desks");
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 4; col++)
            {
                var p = new Vector3(room.xMin + 2.5f + col * 2.6f, 0f, room.yMin + 3f + row * 3f);
                KitPlacer.Prop(desks.transform, "Desk_PC", p, 180f, GameLayers.Prop,
                               name: $"Desk_{row}_{col}");
                KitPlacer.Prop(desks.transform, "Chair_Office", p + new Vector3(0f, 0f, 1.1f), 0f,
                               GameLayers.Prop, name: $"Chair_{row}_{col}");
            }
        }

        /// <summary>
        /// One working light over the entrance.
        ///
        /// Not decoration: it is the only thing that tells a player crossing a foggy wood in the
        /// dark that they are walking towards a door rather than into more trees.
        /// </summary>
        private static void BuildEntranceLight(Transform parent)
        {
            var go = new GameObject("CdsEntranceLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(CdsEntranceX, 3.6f, CdsZSouth - 1.6f);
            go.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 18f;
            light.spotAngle = 95f;
            light.intensity = 4.5f;
            light.color = new Color(1f, 0.74f, 0.44f);
            light.shadows = LightShadows.Hard;

            go.AddComponent<UtezHorror.Environment.FlickeringLight>();
        }
    }
}
