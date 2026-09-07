using UnityEngine;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// Blood on the lens, then the loss screen.
    ///
    /// Two stages, on two different signals, and the split matters. The blood comes in on
    /// <see cref="GameSignals.PlayerDying"/>, which fires the instant the professor reaches you,
    /// so the splatter lands on the impact while the camera is still being turned. The panel
    /// comes in on <see cref="GameSignals.RunLost"/> a couple of seconds later. Show both at
    /// once and the moment of dying is skipped over — the interesting part is the two seconds
    /// where you can see what got you and can no longer do anything about it.
    ///
    /// The panel and the restart used to live here too; they moved to <see cref="RunEndScreen"/>
    /// once the clock running out became a loss as well, so that both ways of ending a run reach
    /// the same screen. This is now only the blood.
    ///
    /// **There are no checkpoints.** Docs/Plans/01 records that the team's GDD rules out saving
    /// entirely, and a checkpoint system would quietly overturn that.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreen : MonoBehaviour
    {
        [SerializeField] private Image blood;
        [SerializeField, Min(0.05f)] private float bloodFadeSeconds = 0.35f;

        private float bloodTarget;
        private float bloodAlpha;

        private void Awake() => Hide();

        private void OnEnable() => GameSignals.PlayerDying += OnDying;

        private void OnDisable() => GameSignals.PlayerDying -= OnDying;

        private void Update()
        {
            if (Mathf.Approximately(bloodAlpha, bloodTarget)) return;

            bloodAlpha = Mathf.MoveTowards(bloodAlpha, bloodTarget, Time.deltaTime / bloodFadeSeconds);
            if (blood == null) return;

            Color colour = blood.color;
            blood.color = new Color(colour.r, colour.g, colour.b, bloodAlpha);
            blood.enabled = bloodAlpha > 0.001f;
        }

        private void Hide()
        {
            bloodTarget = 0f;
            bloodAlpha = 0f;
            if (blood != null) blood.enabled = false;
        }

        private void OnDying(Vector3 killer) => bloodTarget = 1f;

    }
}
