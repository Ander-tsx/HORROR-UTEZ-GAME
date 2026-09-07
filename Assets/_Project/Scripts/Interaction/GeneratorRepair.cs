using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Core.Noise;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// The generator behind the building. Held down to bring the lights back.
    ///
    /// It has been modelled and standing in the yard since the first build with nothing attached
    /// to it. This is what it was for.
    ///
    /// The repair is the game's one guaranteed trap, and every part of it is deliberate: it is
    /// **outside**, so it means crossing the wood; it is **loud**, so the professor knows where
    /// you are; it takes a **long time**, so you cannot glance over your shoulder; and it is a
    /// **fixed location**, so anything hunting you knows exactly where to look. Choosing to fix
    /// the lights is choosing to stand still in the open and make noise for fifteen seconds.
    ///
    /// You are never forced to. Running the rest of the shift in the dark is a legitimate answer,
    /// and that is the decision the whole thing exists to pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratorRepair : MonoBehaviour, IInteractable
    {
        [Tooltip("Seconds of uninterrupted work to bring the mains back.")]
        [SerializeField, Min(1f)] private float repairSeconds = 15f;

        [Tooltip("Hearing radius while cranking it. Deliberately larger than a sprint.")]
        [SerializeField, Min(0f)] private float noiseRadius = 18f;

        [Tooltip("Seconds of not working before the progress drains away.")]
        [SerializeField, Min(0f)] private float patienceSeconds = 2f;

        private float progress;
        private float idleSeconds;
        private float noiseCooldown;

        public float Progress01 => Mathf.Clamp01(progress / repairSeconds);

        private static bool PowerIsOut => PowerSystem.Instance != null && !PowerSystem.Instance.IsPowerOn;

        public string Prompt => PowerIsOut ? "Reparar el generador" : "El generador funciona";

        public bool CanInteract(GameObject interactor) => PowerIsOut && isActiveAndEnabled;

        /// <summary>Held, not pressed — the same verb as a theft, for the same reason.</summary>
        public void Interact(GameObject interactor) { }

        private void Update()
        {
            if (progress <= 0f) return;

            idleSeconds += Time.deltaTime;
            if (idleSeconds >= patienceSeconds)
                progress = Mathf.Max(0f, progress - Time.deltaTime);
        }

        /// <returns>True while there is still work to do.</returns>
        public bool Advance(float deltaSeconds)
        {
            if (!PowerIsOut) return false;

            idleSeconds = 0f;
            progress += deltaSeconds;

            noiseCooldown -= deltaSeconds;
            if (noiseCooldown <= 0f)
            {
                noiseCooldown = 0.6f;
                NoiseSystem.Emit(transform.position, noiseRadius, NoiseSource.Minigame);
            }

            GameSignals.RaiseTheftProgressChanged(Progress01, "Reparando el generador");

            if (progress < repairSeconds) return true;

            progress = 0f;
            PowerSystem.Instance.RestorePower();
            GameSignals.RaiseTheftProgressChanged(-1f, null);
            return false;
        }
    }
}
