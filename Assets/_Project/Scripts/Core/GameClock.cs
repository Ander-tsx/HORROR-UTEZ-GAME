using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>
    /// Single source of truth for how far into the shift the run is. Everything that
    /// escalates over time reads <see cref="Normalised"/> or <see cref="Phase"/> from here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [SerializeField] private ShiftConfig config;
        [SerializeField] private bool startAutomatically = true;

        private float elapsed;
        private bool started;
        private bool paused;
        private bool finished;

        /// <summary>
        /// True only when the shift has begun, is not paused and has not expired.
        /// Pause is tracked separately from "started" so a pause requested before
        /// <see cref="Start"/> runs is not silently discarded by <see cref="StartShift"/>.
        /// </summary>
        public bool IsRunning => started && !paused && !finished && config != null;

        public bool IsPaused => paused;
        public ShiftConfig Config => config;

        /// <summary>Shift progress in 0..1. Reaches 1 exactly when time runs out.</summary>
        public float Normalised => config == null ? 0f : Mathf.Clamp01(elapsed / config.shiftDurationSeconds);

        public float RemainingSeconds => config == null ? 0f : Mathf.Max(0f, config.shiftDurationSeconds - elapsed);

        public ShiftPhase Phase { get; private set; } = ShiftPhase.Arrival;

        /// <summary>In-fiction time of day, for the HUD clock.</summary>
        public float DiegeticHour =>
            config == null ? 0f : Mathf.Lerp(config.startHour, config.endHour, Normalised);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameClock] Duplicate clock on '{name}' — destroying it.", this);
                Destroy(this);
                return;
            }

            Instance = this;

            if (config == null)
                Debug.LogError("[GameClock] No ShiftConfig assigned; the clock will not advance.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (startAutomatically) StartShift();
        }

        /// <summary>
        /// Injects the tuning to run with. Lets a run be started from a difficulty choice
        /// or a test without the clock having to be authored in a scene first.
        /// </summary>
        public void Configure(ShiftConfig newConfig)
        {
            config = newConfig;
        }

        public void StartShift()
        {
            elapsed = 0f;
            started = true;
            finished = false;
            Phase = ShiftPhase.Arrival;
            GameSignals.RaisePhaseChanged(Phase);
            GameSignals.RaiseShiftProgressChanged(0f);
        }

        public void SetPaused(bool value) => paused = value;

        private void Update()
        {
            if (!IsRunning) return;

            elapsed += Time.deltaTime;
            GameSignals.RaiseShiftProgressChanged(Normalised);

            ShiftPhase next = config.PhaseAt(Normalised);
            if (next != Phase)
            {
                Phase = next;
                GameSignals.RaisePhaseChanged(Phase);
            }

            if (elapsed >= config.shiftDurationSeconds)
            {
                finished = true;
                GameSignals.RaiseRunEnded(RunOutcome.TimeExpired);

            // Also on RunLost, which is what the loss screen listens to. RunEnded predates it
            // and other systems already depend on that name, so both are raised rather than one
            // being renamed out from under them.
            GameSignals.RaiseRunLost(RunOutcome.TimeExpired);
            }
        }
    }
}
