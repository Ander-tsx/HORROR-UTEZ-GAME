using UnityEngine;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// How exposed the player is, as a row of blocks that fills upward and warms in colour.
    ///
    /// Its whole job is to make the stealth rules **legible**. Crouching, sprinting and the torch
    /// each change how far away you can be seen, and without a meter the player has no way to
    /// learn that beyond dying repeatedly and guessing. A stealth system nobody can read is not
    /// a stealth system, it is a random punishment.
    ///
    /// It shows exactly the number the enemy acts on (both come from
    /// <see cref="GameSignals.PlayerVisibilityChanged"/>), because a meter that disagrees with
    /// the thing hunting you teaches the wrong lesson.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibilityMeter : MonoBehaviour
    {
        [SerializeField] private Image[] segments;

        [SerializeField] private Color hidden = new(0.55f, 0.72f, 0.55f, 0.75f);
        [SerializeField] private Color exposed = new(0.90f, 0.42f, 0.30f, 0.95f);
        [SerializeField] private Color unlit = new(0.16f, 0.18f, 0.16f, 0.45f);

        private float exposure;

        private void OnEnable()
        {
            GameSignals.PlayerVisibilityChanged += SetExposure;
            Redraw();
        }

        private void OnDisable() => GameSignals.PlayerVisibilityChanged -= SetExposure;

        public void SetExposure(float value01)
        {
            exposure = Mathf.Clamp01(value01);
            Redraw();
        }

        private void Redraw()
        {
            if (segments == null || segments.Length == 0) return;

            // Round, not ceil: unlike the battery there is no "any at all still works" case, and
            // an eye meter that never empties would imply you are never properly hidden.
            int lit = Mathf.RoundToInt(exposure * segments.Length);

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;

                if (i >= lit)
                {
                    segments[i].color = unlit;
                    continue;
                }

                // Each block carries its own colour along the ramp, so the meter reads as a
                // gradient from safe to caught rather than as a bar that flips colour at a
                // threshold the player cannot see.
                float t = segments.Length == 1 ? 1f : i / (float)(segments.Length - 1);
                segments[i].color = Color.Lerp(hidden, exposed, t);
            }
        }
    }
}
