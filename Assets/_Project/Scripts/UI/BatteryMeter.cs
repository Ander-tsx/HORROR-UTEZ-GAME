using UnityEngine;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// The phone torch's charge, drawn as a row of solid blocks.
    ///
    /// Blocks rather than a bar or a percentage, for three reasons. A continuous bar at 360p is
    /// four pixels of gradient and unreadable. A number needs a font, and every font in this
    /// project would have to be a bitmap one to survive the resolution. And a phone battery icon
    /// is *already* segmented, so the shape reads instantly without a label.
    ///
    /// It also blinks the last block when the charge is nearly gone. That is the only warning
    /// the player gets before the building goes completely dark, so it has to be impossible to
    /// miss without being loud enough to break the mood.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BatteryMeter : MonoBehaviour
    {
        [SerializeField] private Image[] segments;

        [Header("Colours")]
        [SerializeField] private Color full = new(0.78f, 0.86f, 0.74f, 0.85f);
        [SerializeField] private Color low = new(0.85f, 0.55f, 0.30f, 0.9f);
        [SerializeField] private Color empty = new(0.16f, 0.18f, 0.16f, 0.5f);

        [Header("Warning")]
        [Tooltip("Charge below which the last remaining block blinks.")]
        [SerializeField, Range(0f, 0.5f)] private float lowThreshold = 0.25f;

        [SerializeField, Min(0.1f)] private float blinkPeriod = 0.9f;

        private float charge01 = 1f;

        private void OnEnable()
        {
            GameSignals.FlashlightBatteryChanged += SetCharge;
            Redraw();
        }

        private void OnDisable() => GameSignals.FlashlightBatteryChanged -= SetCharge;

        /// <summary>Also usable straight from a UnityEvent on ShiftHudPresenter.</summary>
        public void SetCharge(float value01)
        {
            charge01 = Mathf.Clamp01(value01);
            Redraw();
        }

        private void Update()
        {
            if (charge01 > lowThreshold || segments == null || segments.Length == 0) return;
            Redraw();
        }

        private void Redraw()
        {
            if (segments == null || segments.Length == 0) return;

            // Ceil, so any charge at all keeps one block lit. Rounding down would show an empty
            // meter while the torch still works, which reads as a bug.
            int lit = charge01 <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(charge01 * segments.Length),
                                                       1, segments.Length);
            bool warning = charge01 > 0f && charge01 <= lowThreshold;
            bool blinkOn = !warning || Mathf.Repeat(Time.unscaledTime, blinkPeriod) < blinkPeriod * 0.6f;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;

                bool isLit = i < lit;
                bool isLast = i == lit - 1;

                Color colour = !isLit ? empty : warning ? low : full;
                if (isLit && isLast && warning && !blinkOn) colour = empty;

                segments[i].color = colour;
            }
        }
    }
}
