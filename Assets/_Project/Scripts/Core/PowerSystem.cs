using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>
    /// Mains power for the building. A blackout is the design's main lever: it darkens the
    /// space, forces the torch on (burning battery), and gates which enemies may appear.
    /// Kept as one authority so lighting, audio and the enemy director can never disagree
    /// about whether the lights are on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerSystem : MonoBehaviour
    {
        public static PowerSystem Instance { get; private set; }

        [SerializeField] private bool powerOnAtStart = true;

        [Tooltip("Seconds the generator must stay repaired before it can fail again.")]
        [SerializeField, Min(0f)] private float minSecondsBetweenOutages = 90f;

        private float earliestNextOutage;

        public bool IsPowerOn { get; private set; } = true;

        /// <summary>False while the cooldown since the last repair has not elapsed.</summary>
        public bool CanCutPower => IsPowerOn && Time.time >= earliestNextOutage;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[PowerSystem] Duplicate on '{name}' — destroying it.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            IsPowerOn = powerOnAtStart;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => GameSignals.RaisePowerChanged(IsPowerOn);

        /// <summary>Returns false if an outage is not allowed yet.</summary>
        public bool CutPower()
        {
            if (!CanCutPower) return false;
            SetPower(false);
            return true;
        }

        /// <summary>Called when the generator repair completes.</summary>
        public void RestorePower()
        {
            if (IsPowerOn) return;
            earliestNextOutage = Time.time + minSecondsBetweenOutages;
            SetPower(true);
        }

        private void SetPower(bool on)
        {
            if (IsPowerOn == on) return;
            IsPowerOn = on;
            GameSignals.RaisePowerChanged(on);
        }
    }
}
