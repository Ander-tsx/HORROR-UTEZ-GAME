using UnityEngine;
using UnityEngine.Events;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// Formats run state into display strings and pushes them out through UnityEvents.
    /// Deliberately knows nothing about uGUI or UI Toolkit: the widget choice can change
    /// later without touching any game logic, and the same presenter drives the PC HUD
    /// and the larger touch HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShiftHudPresenter : MonoBehaviour
    {
        [SerializeField] private UnityEvent<string> onClockText;
        [SerializeField] private UnityEvent<float> onShiftProgress;
        [SerializeField] private UnityEvent<float> onBatteryLevel;
        [SerializeField] private UnityEvent<string> onPhaseText;

        private void OnEnable()
        {
            GameSignals.ShiftProgressChanged += HandleProgress;
            GameSignals.PhaseChanged += HandlePhase;
            GameSignals.FlashlightBatteryChanged += HandleBattery;
        }

        private void OnDisable()
        {
            GameSignals.ShiftProgressChanged -= HandleProgress;
            GameSignals.PhaseChanged -= HandlePhase;
            GameSignals.FlashlightBatteryChanged -= HandleBattery;
        }

        private void HandleProgress(float normalised)
        {
            onShiftProgress?.Invoke(normalised);

            GameClock clock = GameClock.Instance;
            if (clock == null) return;

            float hour = clock.DiegeticHour;
            int hours = Mathf.FloorToInt(hour);
            int minutes = Mathf.FloorToInt((hour - hours) * 60f);
            onClockText?.Invoke($"{hours:00}:{minutes:00}");
        }

        private void HandlePhase(ShiftPhase phase) => onPhaseText?.Invoke(phase.ToString());

        private void HandleBattery(float level01) => onBatteryLevel?.Invoke(level01);
    }
}
