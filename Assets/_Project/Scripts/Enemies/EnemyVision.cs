using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Utils;

namespace UtezHorror.Enemies
{
    /// <summary>
    /// Line of sight against a single target. Cone, range, and an unobstructed view.
    ///
    /// Kept separate from the state machine for the same reason as <see cref="EnemyHearing"/>:
    /// perception answers "can I see it", never "what should I do about it". That split is what
    /// lets the interesting numbers live in <see cref="EnemyConfig"/> rather than in AI code.
    ///
    /// Darkness by itself does not hide the player, and that is deliberate: an enemy that cannot
    /// see an unlit player is one you defeat by switching off and walking slowly, which deletes
    /// the tension rather than creating it. What *does* change the range is what the player is
    /// doing — crouching, sprinting, standing still, torch on or off — which arrives as a single
    /// exposure value from <see cref="GameSignals.PlayerVisibilityChanged"/>. Stealth is a set of
    /// choices with costs, not a light switch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVision : MonoBehaviour
    {
        [SerializeField] private EnemyConfig config;

        [Tooltip("Where the eyes are. Falls back to a point above the transform.")]
        [SerializeField] private Transform eyes;

        [Tooltip("Seconds between sight checks. Vision does not need to run every frame.")]
        [SerializeField, Min(0f)] private float scanInterval = 0.15f;

        [Header("Exposure")]
        [Tooltip("Sight range multiplier when the player is doing everything right.")]
        [SerializeField, Range(0.1f, 1f)] private float minRangeFactor = 0.35f;

        [Tooltip("Sight range multiplier when the player is sprinting with the torch on.")]
        [SerializeField, Range(1f, 2f)] private float maxRangeFactor = 1.35f;

        private Transform target;
        private float nextScanTime;
        private float exposure = 0.5f;

        /// <summary>Sight range as it currently is, after the player's exposure is applied.</summary>
        public float EffectiveRange => config == null
            ? 0f
            : config.sightRange * Mathf.Lerp(minRangeFactor, maxRangeFactor, exposure);

        /// <summary>True as of the last scan.</summary>
        public bool HasLineOfSight { get; private set; }

        /// <summary>Where the target was when it was last seen. Only meaningful once seen.</summary>
        public Vector3 LastSeenPosition { get; private set; }

        public Vector3 EyePosition => eyes != null ? eyes.position : transform.position + Vector3.up * 1.6f;

        public void SetConfig(EnemyConfig value) => config = value;

        private void Awake()
        {
            if (eyes == null) eyes = transform;
        }

        private void OnEnable() => GameSignals.PlayerVisibilityChanged += OnExposureChanged;

        private void OnDisable() => GameSignals.PlayerVisibilityChanged -= OnExposureChanged;

        private void OnExposureChanged(float value) => exposure = Mathf.Clamp01(value);

        private void Update()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + scanInterval;

            HasLineOfSight = CanSeeTarget();
            if (HasLineOfSight && target != null) LastSeenPosition = target.position;
        }

        private bool CanSeeTarget()
        {
            if (config == null) return false;
            if (!ResolveTarget()) return false;

            // Aim at the chest, not the feet: a floor-level ray is blocked by every desk.
            Vector3 origin = EyePosition;
            Vector3 targetPoint = target.position + Vector3.up * 1.2f;
            Vector3 delta = targetPoint - origin;

            float distance = delta.magnitude;
            if (distance > EffectiveRange) return false;

            Vector3 direction = delta / Mathf.Max(distance, 1e-4f);
            if (Vector3.Angle(transform.forward, direction) > config.sightAngle * 0.5f) return false;

            // OcclusionMask, the same set that muffles noise, so what blocks sound blocks sight.
            // A door counts, which is what makes closing one behind you worth doing.
            return !Physics.Raycast(origin, direction, distance, GameLayers.OcclusionMask,
                                    QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Finds the player once and keeps it. Uses the camera tag rather than a name lookup so
        /// it survives the player object being renamed, and so it works in either scene.
        /// </summary>
        private bool ResolveTarget()
        {
            if (target != null) return true;

            Camera main = Camera.main;
            if (main == null) return false;

            // The camera hangs off the player root; the root is what moves and what to chase.
            target = main.transform.parent != null ? main.transform.parent : main.transform;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null) return;

            Gizmos.color = HasLineOfSight ? Color.red : new Color(1f, 0.8f, 0.2f, 0.6f);
            Vector3 origin = EyePosition;
            float half = config.sightAngle * 0.5f;

            for (float a = -half; a <= half; a += 10f)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * transform.forward;
                Gizmos.DrawLine(origin, origin + dir * EffectiveRange);
            }
        }
    }
}
