using UnityEngine;

namespace UtezHorror.Environment
{
    /// <summary>One fog and ambient setting.</summary>
    [System.Serializable]
    public struct Atmosphere
    {
        public Color fogColor;
        [Min(0f)] public float fogDensity;
        public Color ambient;

        public static Atmosphere Lerp(Atmosphere a, Atmosphere b, float t) => new()
        {
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
            ambient = Color.Lerp(a.ambient, b.ambient, t)
        };
    }

    /// <summary>
    /// The project's four atmospheres, in one place.
    ///
    /// Shared rather than duplicated because three different systems need the same numbers:
    /// <see cref="AtmosphereController"/> blends between them at runtime, the scene builder
    /// stamps one of them onto the saved scene so opening it in the editor is not a black
    /// rectangle, and the screenshot tool applies the right one per shot so a capture of the
    /// wood is not lit like a corridor. When they were separate values, they drifted.
    ///
    /// Indoors is near-black with barely any fog: the torch is the only light, and fog would only
    /// wash out its beam. Outdoors is thick, visible, grey-green — the Silent Hill signature, the
    /// draw-distance cap that lets a wood run on a phone, and the reason something can step out
    /// of nowhere. Crossing a doorway is meant to read as changing worlds.
    /// </summary>
    public static class AtmospherePresets
    {
        public static readonly Atmosphere InteriorEarly = new()
        {
            fogColor = new Color(0.055f, 0.062f, 0.055f),
            fogDensity = 0.020f,
            ambient = new Color(0.042f, 0.047f, 0.043f)
        };

        public static readonly Atmosphere InteriorLate = new()
        {
            fogColor = new Color(0.012f, 0.014f, 0.013f),
            fogDensity = 0.030f,
            ambient = new Color(0.012f, 0.014f, 0.013f)
        };

        public static readonly Atmosphere ExteriorEarly = new()
        {
            // Bright enough to read as fog against the trees, dark enough to still be night.
            // At 0.32 it looked like daylight haze and flattened the whole wood into one grey.
            fogColor = new Color(0.150f, 0.170f, 0.142f),
            fogDensity = 0.038f,
            ambient = new Color(0.072f, 0.082f, 0.070f)
        };

        public static readonly Atmosphere ExteriorLate = new()
        {
            fogColor = new Color(0.040f, 0.050f, 0.042f),
            fogDensity = 0.075f,
            ambient = new Color(0.026f, 0.031f, 0.026f)
        };

        /// <summary>Applies one directly to the scene's render settings.</summary>
        public static void Apply(Atmosphere atmosphere)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = atmosphere.fogColor;
            RenderSettings.fogDensity = atmosphere.fogDensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = atmosphere.ambient;
            RenderSettings.ambientIntensity = 1f;
        }
    }
}
