using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>
    /// Tuning for the one-shift run. Lives as an asset so difficulty can be retuned
    /// without touching code, and so alternate runs (a short demo build, a playtest
    /// build) are just a different asset.
    /// </summary>
    [CreateAssetMenu(menuName = "UtezHorror/Shift Config", fileName = "ShiftConfig")]
    public sealed class ShiftConfig : ScriptableObject
    {
        [Tooltip("Real-time length of a full shift, in seconds.")]
        [Min(30f)] public float shiftDurationSeconds = 25f * 60f;

        [Header("Phase thresholds (normalised shift progress)")]
        [Tooltip("Progress at which Arrival gives way to Fading.")]
        [Range(0f, 1f)] public float fadingStart = 0.15f;

        [Tooltip("Progress at which Fading gives way to Dark.")]
        [Range(0f, 1f)] public float darkStart = 0.40f;

        [Tooltip("Progress at which Dark gives way to Curfew.")]
        [Range(0f, 1f)] public float curfewStart = 0.75f;

        [Header("Diegetic clock")]
        [Tooltip("In-fiction hour shown to the player at progress 0.")]
        [Range(0f, 24f)] public float startHour = 17f;

        [Tooltip("In-fiction hour shown to the player at progress 1.")]
        [Range(0f, 24f)] public float endHour = 23f;

        public ShiftPhase PhaseAt(float normalised)
        {
            if (normalised >= curfewStart) return ShiftPhase.Curfew;
            if (normalised >= darkStart) return ShiftPhase.Dark;
            if (normalised >= fadingStart) return ShiftPhase.Fading;
            return ShiftPhase.Arrival;
        }

        private void OnValidate()
        {
            // Keep the thresholds monotonic; an out-of-order set would make a phase unreachable.
            darkStart = Mathf.Max(darkStart, fadingStart);
            curfewStart = Mathf.Max(curfewStart, darkStart);
        }
    }
}
