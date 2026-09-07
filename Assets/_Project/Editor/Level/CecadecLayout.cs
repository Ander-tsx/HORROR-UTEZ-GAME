using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// The Cecadec ground-floor plan, in metres, transcribed from the team's topographic map.
    ///
    /// Axes: +X runs east (right on the map), +Z runs north (up on the map). The origin is the
    /// building's south-west interior corner, so every number below can be read straight off
    /// the drawing. Wall positions are centre lines.
    /// </summary>
    public static class CecadecLayout
    {
        // ---- vertical grid lines (X) ----
        public const float XWest = 0f;          // west exterior wall
        public const float XWestBlock = 12f;  // west rooms / corridor
        public const float XEastBlock = 21f;  // corridor / east rooms
        public const float XEast = 33f;         // east exterior wall

        // ---- horizontal grid lines (Z) ----
        public const float ZSouth = 0f;         // south exterior wall
        public const float ZSouthRooms = 14f; // south rooms / cross corridor
        public const float ZCrossNorth = 23f; // cross corridor / north rooms
        public const float ZRoomSplit = 47f;  // aula normal / centro de cómputo
        public const float ZNorth = 61f;      // north exterior wall

        /// <summary>
        /// The modular grid. Every wall run, room and door position is a whole number of
        /// metres so the 4 m / 2 m / 1 m kit pieces compose without gaps or stretched fillers.
        /// Snap the layout to the module, never the module to the layout.
        /// </summary>
        public const float Module = 1f;

        // ---- build parameters ----
        public const float CeilingHeight = 3.2f;
        public const float SlabThickness = 0.35f;
        public const float ExteriorWallThickness = 0.35f;
        public const float InteriorWallThickness = 0.22f;

        public const float DoorWidth = 1.15f;
        public const float DoorHeight = 2.15f;
        public const float GlassDoorWidth = 2.80f;
        public const float GlassDoorHeight = 2.45f;

        /// <summary>Metres of open ground modelled around the building.</summary>
        public const float ExteriorMargin = 16f;

        // ---- corridor width, the thing the team asked to triple ----
        public static float MainCorridorWidth => XEastBlock - XWestBlock;   // 9.1 m
        public static float CrossCorridorDepth => ZCrossNorth - ZSouthRooms; // 9.1 m

        // ---- door positions along their wall ----
        public const float ZDoorAulaNormal = 49f;     // east wall of the north corridor
        public const float ZDoorCentroComputo = 26f;  // east wall of the north corridor
        public const float ZDoorServiciosBlocked = 56f;
        public const float ZDoorAulaComputoWest = 11f;
        public const float ZDoorAulaComputoEast = 11f;

        public static float XMainEntrance => 17f;   // north facade, on a module centre
        public static float ZSideEntrance => 18f;   // east facade, on a module centre

        // ---- stairwell: square spiral in the west end of the cross corridor ----
        public static readonly Vector2 StairCentre = new(5f, 19f);
        public const float StairOuterSize = 4f;
        public const float StairRunWidth = 1.2f;

        // ---- generator, outside the north-west corner ----
        public static readonly Vector3 GeneratorPosition = new(5.5f, 0f, 66f);

        /// <summary>
        /// The handful of places in the building where the light still works, chosen by hand.
        ///
        /// The fittings used to be dealt out at random with ~92% dead, which produced *uniform*
        /// darkness — and uniform is monotonous, not frightening. Fear lives in contrast: if the
        /// whole corridor is equally black the torch reveals nothing, it just drags a grey circle
        /// around. These are the pools the darkness in between is measured against, so they are
        /// placed at the points a player navigates by: the entrance, the junction, the stair, the
        /// far end of a long run.
        ///
        /// Positions are XZ and apply to both floors.
        /// </summary>
        public static readonly Vector2[] LightPools =
        {
            new(17f, 57f),    // north corridor by the main doors: the first thing you ever see
            new(17f, 26f),    // far end of the same corridor, outside the centro de cómputo
            new(16f, 18.5f),  // the cross-corridor junction, where you choose a direction
            new(5f, 19f),     // the stairwell: a vertical route has to be findable
            new(27f, 34f),    // deep inside the centro de cómputo, so the room has a far wall
            new(6f, 7f),      // south-west aula de cómputo

            // Added 2026-09-06: rooms were navigable only by torch, which made every one of them
            // read the same. A working fitting per room is what gives a space a shape you can
            // remember and come back to. They are dim, not bright — see CeilingFixture.
            new(27f, 54f),    // aula normal
            new(6f, 42f),     // servicios escolares
            new(27f, 7f),     // south-east aula de cómputo
            new(27f, 40f),    // far end of the centro de cómputo
        };

        /// <summary>Metres from a pool centre within which a fitting is lit and steady.</summary>
        public const float LightPoolRadius = 5.5f;

        /// <summary>Beyond this, fittings are always dead. Between the two, some flicker.</summary>
        public const float LightPoolFringe = 11f;

        /// <summary>Where the player starts: outside, facing the main glass doors.</summary>
        public static readonly Vector3 PlayerSpawn = new(17f, 0.15f, 69f);

        public static float FloorToFloor => CeilingHeight + SlabThickness;

        public static Rect Room(float xMin, float zMin, float xMax, float zMax) =>
            Rect.MinMaxRect(xMin, zMin, xMax, zMax);

        // Room footprints, interior faces ignored for simplicity (walls are centred on the lines).
        public static Rect ServiciosEscolares => Room(XWest, ZCrossNorth, XWestBlock, ZNorth);
        public static Rect AulaNormal => Room(XEastBlock, ZRoomSplit, XEast, ZNorth);
        public static Rect CentroComputo => Room(XEastBlock, ZCrossNorth, XEast, ZRoomSplit);
        public static Rect AulaComputoOeste => Room(XWest, ZSouth, XWestBlock, ZSouthRooms);
        public static Rect AulaComputoEste => Room(XEastBlock, ZSouth, XEast, ZSouthRooms);

        public static Rect CorridorNorth => Room(XWestBlock, ZCrossNorth, XEastBlock, ZNorth);
        public static Rect CorridorCross => Room(XWest, ZSouthRooms, XEast, ZCrossNorth);
        public static Rect CorridorSouth => Room(XWestBlock, ZSouth, XEastBlock, ZSouthRooms);
    }
}
