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
        /// Interior: warm amber-tinged darkness. The torch is still needed — the lights
        /// provide just enough glow to know you're indoors. Puddles on the floor reflect
        /// the flickering tubes. Fog is almost absent so the torch beam carries far.
        public static readonly Atmosphere InteriorEarly = new()
        {
            fogColor  = new Color(0.028f, 0.022f, 0.018f),
            fogDensity = 0.012f,
            ambient   = new Color(0.018f, 0.014f, 0.010f)  // near-black warm tint
        };

        public static readonly Atmosphere InteriorLate = new()
        {
            fogColor  = new Color(0.008f, 0.006f, 0.005f),
            fogDensity = 0.022f,
            ambient   = new Color(0.006f, 0.005f, 0.004f)
        };

        /// Exterior: essentially pitch black. Flashlight is mandatory.
        /// Dense fog eats trees beyond ~12m and hides the world edge completely.
        public static readonly Atmosphere ExteriorEarly = new()
        {
            fogColor  = new Color(0.012f, 0.018f, 0.014f),  // barely visible dark-green
            fogDensity = 0.065f,                             // was 0.038 — very thick now
            ambient   = new Color(0.008f, 0.010f, 0.008f)   // almost no ambient, torch required
        };

        public static readonly Atmosphere ExteriorLate = new()
        {
            fogColor  = new Color(0.004f, 0.006f, 0.005f),
            fogDensity = 0.110f,                             // end-game fog is suffocating
            ambient   = new Color(0.003f, 0.004f, 0.003f)
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
