using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// The outdoor world: the ground the two buildings stand on, and where CDS sits.
    ///
    /// Same contract as <see cref="CecadecLayout"/> — these numbers are the source of truth and
    /// the scene is rebuilt from them, so editing the scene by hand does not survive.
    ///
    /// On the distance between the buildings: in reality CDS is right next to Cecadec. It is
    /// pushed much further away here on purpose, so that crossing between them is a walk through
    /// fog and trees with something at stake, and so the fog is visibly doing work rather than
    /// being an invisible draw-distance cap. This is a design dial, not a fact about the campus.
    /// </summary>
    public static class ExteriorLayout
    {
        // ---- the modelled world, in Cecadec's coordinate frame ----
        public static readonly Vector2 WorldMin = new(-52f, -42f);
        public static readonly Vector2 WorldMax = new(86f, 142f);

        /// <summary>
        /// Terrain grid step. Matches the level's quad limit so the ground snaps and warps at the
        /// same rate as the architecture; a coarser ground would visibly wobble less than the
        /// walls standing on it.
        /// </summary>
        public const float GridStep = 1.5f;

        // ---- relief ----
        public const float HillAmplitude = 2.1f;
        public const float HillWavelength = 26f;
        public const float BumpAmplitude = 0.45f;
        public const float BumpWavelength = 6.5f;

        /// <summary>Fixed so the ground does not reshuffle between builds.</summary>
        public const int TerrainSeed = 20260906;

        /// <summary>
        /// Height every building pad is flattened to. Just below the pavement apron (-0.12) so
        /// the apron reads as laid on top of the ground rather than sunk into it.
        /// </summary>
        public const float PadHeight = -0.22f;

        /// <summary>Metres over which a pad blends back into the natural ground.</summary>
        public const float PadFalloff = 9f;

        // ---- CDS: north of Cecadec, on the generator side, across the wood ----
        public const float CdsXWest = -14f;
        public const float CdsXEast = 14f;
        public const float CdsZSouth = 104f;
        public const float CdsZNorth = 130f;

        /// <summary>The one door into CDS that opens, on its south face, looking back at Cecadec.</summary>
        public static float CdsEntranceX => 4f;

        /// <summary>The single room behind that door. The rest of the building is sealed.</summary>
        public static Rect CdsOpenRoom => Rect.MinMaxRect(CdsXWest, CdsZSouth, CdsXWest + 14f, CdsZSouth + 12f);

        public static Rect CdsFootprint => Rect.MinMaxRect(CdsXWest, CdsZSouth, CdsXEast, CdsZNorth);

        public static Rect CecadecFootprint => Rect.MinMaxRect(
            CecadecLayout.XWest, CecadecLayout.ZSouth, CecadecLayout.XEast, CecadecLayout.ZNorth);

        /// <summary>Pads are the footprint plus room to stand, or the player falls off the edge.</summary>
        public static Rect CecadecPad => Grow(CecadecFootprint, 11f);
        public static Rect CdsPad => Grow(CdsFootprint, 8f);

        /// <summary>
        /// The worn path between the two entrances. Flattened and kept clear of trees: an
        /// unreadable wood with no route through it is disorienting rather than frightening, and
        /// the player has to be able to find their way back.
        /// </summary>
        public static readonly Vector2 PathFrom = new(CecadecLayout.XMainEntrance, CecadecLayout.ZNorth + 6f);
        public static readonly Vector2 PathTo = new(CdsEntranceX, CdsZSouth - 5f);
        public const float PathHalfWidth = 2.6f;

        // ---- the wood ----
        public const float TreeSpacing = 7.5f;
        public const float TreeJitter = 3.2f;

        /// <summary>Metres of clearance kept around pads and the path.</summary>
        public const float TreeClearance = 4f;

        public static Rect Grow(Rect r, float by) =>
            Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        /// <summary>Shortest distance from a point to the path's centre line.</summary>
        public static float DistanceToPath(Vector2 p)
        {
            Vector2 a = PathFrom, b = PathTo;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
