using UnityEngine;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// Shows the verb for whatever the player is aiming at, and a reticle dot that thickens
    /// when there is something to act on.
    ///
    /// The dot matters more than the text at this resolution: a word rendered into a 360p
    /// buffer is barely legible, but a crosshair changing state is readable instantly and in
    /// any language. The text is the explanation, the dot is the signal.
    ///
    /// Listens on <see cref="GameSignals"/> rather than to PlayerInteractor directly, so the UI
    /// assembly does not have to know the interaction system exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image reticle;

        [SerializeField] private Color idleReticle = new(0.75f, 0.78f, 0.72f, 0.30f);
        [SerializeField] private Color activeReticle = new(0.95f, 0.93f, 0.80f, 0.95f);

        [Tooltip("Reticle size in pixels when idle, and when something can be acted on.")]
        [SerializeField] private float idleSize = 3f;
        [SerializeField] private float activeSize = 7f;

        private void OnEnable()
        {
            GameSignals.InteractionPromptChanged += Show;
            Show(null);
        }

        private void OnDisable() => GameSignals.InteractionPromptChanged -= Show;

        /// <summary>Null or empty hides the prompt.</summary>
        public void Show(string prompt)
        {
            bool active = !string.IsNullOrEmpty(prompt);

            if (label != null)
            {
                label.text = prompt ?? string.Empty;
                label.enabled = active;
            }

            if (reticle == null) return;

            reticle.color = active ? activeReticle : idleReticle;
            float size = active ? activeSize : idleSize;
            reticle.rectTransform.sizeDelta = new Vector2(size, size);
        }
    }
}
