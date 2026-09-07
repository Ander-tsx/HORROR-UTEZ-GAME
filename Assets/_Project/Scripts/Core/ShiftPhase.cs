namespace UtezHorror.Core
{
    /// <summary>
    /// Coarse stages of the single playable shift. Everything that ramps difficulty
    /// (light level, enemy availability, event frequency) keys off this rather than off
    /// a raw clock value, so the curve can be retuned in one place.
    /// </summary>
    public enum ShiftPhase
    {
        /// <summary>Doors just sealed. Full light, no threats. Teaches the space.</summary>
        Arrival = 0,

        /// <summary>Light starts draining. First scripted events become legal.</summary>
        Fading = 1,

        /// <summary>Building is dark. Flashlight required. Events fire on a schedule.</summary>
        Dark = 2,

        /// <summary>Final stretch. Highest pressure, shortest gaps between events.</summary>
        Curfew = 3
    }
}
