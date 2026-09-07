using UnityEngine;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// The two dials that decide how strong the PS1 distortion is, in one place.
    ///
    /// They are here rather than left on each material because they have to match across every
    /// surface: a wall wobbling on a different grid from the floor it meets reads as a seam
    /// opening and closing, not as a style. Change them here and rebuild.
    ///
    /// Both are also the comfort dials. The distortion is only visible while the camera moves —
    /// standing still, every vertex lands in the same cell every frame, which is exactly how the
    /// console behaved — so the whole effect is motion, and too much of it is nausea rather than
    /// atmosphere.
    /// </summary>
    public static class PS1Look
    {
        /// <summary>
        /// Virtual screen grid the vertices snap to.
        ///
        /// Read it against the render resolution (360p, so ~640x360): this grid decides how far a
        /// vertex jumps when it changes cell. At 320x240 a jump is about two render pixels. The
        /// first pass used 160x120, which is four render pixels — roughly twelve on a 1080p
        /// monitor — and that is a lurch, not a wobble.
        ///
        /// Lower = more period-accurate and more sickening. Raise it towards 640x360 and the
        /// effect fades to nothing, because the snap grid becomes the pixel grid.
        /// </summary>
        public static readonly Vector2 JitterResolution = new(320f, 240f);

        /// <summary>
        /// How much the texture coordinate is interpolated without perspective correction.
        /// 1 is period-accurate; the texture visibly swims and shears on every large surface.
        ///
        /// Kept well below 1 because it stacks with the vertex snap: both only appear while
        /// moving, so they add up into the same sensation. The warp is most visible on floors and
        /// long walls, which is most of what a player looks at.
        /// </summary>
        public const float AffineAmount = 0.45f;

        /// <summary>Foliage warps less: leaves are small quads where the shear reads as noise.</summary>
        public const float FoliageAffineAmount = 0.25f;

        /// <summary>Writes the dials onto a material using one of the PS1 shaders.</summary>
        public static void Apply(Material material, float affineAmount = AffineAmount)
        {
            if (material == null) return;
            material.SetVector("_JitterResolution", new Vector4(JitterResolution.x, JitterResolution.y, 0f, 0f));
            material.SetFloat("_AffineAmount", affineAmount);
        }
    }
}
