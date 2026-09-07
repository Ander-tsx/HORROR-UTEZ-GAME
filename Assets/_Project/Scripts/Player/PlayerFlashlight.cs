using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.Player
{
    /// <summary>
    /// The phone torch. Battery is the run's only consumable resource: it is what forces
    /// the player back to the charge point, which is a fixed, exposed location.
    /// </summary>
    [RequireComponent(typeof(PlayerInputRouter))]
    [DisallowMultipleComponent]
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] private Light beam;

        [Tooltip("Seconds of continuous light from a full battery.")]
        [SerializeField, Min(1f)] private float fullChargeSeconds = 240f;

        [SerializeField, Range(0f, 1f)] private float startCharge = 1f;

        private PlayerInputRouter input;
        private float charge01;

        public bool IsOn => beam != null && beam.enabled;

        /// <summary>Remaining battery, 0..1.</summary>
        public float Charge01 => charge01;

        private void Awake()
        {
            input = GetComponent<PlayerInputRouter>();
            if (beam == null) beam = GetComponentInChildren<Light>(includeInactive: true);
            charge01 = startCharge;
        }

        private void OnEnable()
        {
            input.FlashlightPressed += Toggle;
            GameSignals.BatteryPickedUp += Recharge;
            SetBeam(false);
            GameSignals.RaiseFlashlightBatteryChanged(charge01);
        }

        private void OnDisable()
        {
            input.FlashlightPressed -= Toggle;
            GameSignals.BatteryPickedUp -= Recharge;
        }

        private void Update()
        {
            if (!IsOn) return;

            charge01 = Mathf.Max(0f, charge01 - Time.deltaTime / fullChargeSeconds);
            GameSignals.RaiseFlashlightBatteryChanged(charge01);

            if (charge01 <= 0f) SetBeam(false);
        }

        public void Toggle()
        {
            if (!IsOn && charge01 <= 0f) return;
            SetBeam(!IsOn);
        }

        /// <summary>Called by the charge point. <paramref name="amount01"/> is a fraction of a full battery.</summary>
        public void Recharge(float amount01)
        {
            charge01 = Mathf.Clamp01(charge01 + amount01);
            GameSignals.RaiseFlashlightBatteryChanged(charge01);
        }

        private void SetBeam(bool on)
        {
            if (beam != null) beam.enabled = on;
        }
    }
}
