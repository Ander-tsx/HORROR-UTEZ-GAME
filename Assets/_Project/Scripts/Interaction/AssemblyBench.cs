using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Core.Noise;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// Where the run ends: the bench you build your own machine on.
    ///
    /// Docs/Plans/01 argues for this over a counter reaching 4/4, and the argument is that the
    /// fantasy is *building the computer*, not collecting parts. A win screen that fires the
    /// instant the last component enters your bag ends the game at its least interesting moment
    /// — the moment you stop being in danger.
    ///
    /// So the climax is a fixed, known location, it takes real time, and it is only available
    /// once you are carrying everything. That last part is what makes it work: you assemble it
    /// while holding the cabinet you cannot run with, at the point in the shift when the director
    /// is spending its budget fastest. The safest thing you own is also the thing you have to
    /// stand still next to.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyBench : MonoBehaviour, IInteractable
    {
        [Tooltip("Seconds of uninterrupted work to build the machine.")]
        [SerializeField, Min(1f)] private float assembleSeconds = 20f;

        [Tooltip("Hearing radius while working. Loud: this is the last thing you do, not a hiding place.")]
        [SerializeField, Min(0f)] private float noiseRadius = 12f;

        [SerializeField, Min(0f)] private float patienceSeconds = 2.5f;

        private float progress;
        private float idleSeconds;
        private float noiseCooldown;
        private bool finished;

        public float Progress01 => Mathf.Clamp01(progress / assembleSeconds);

        private static bool HasEverything =>
            ObjectiveSystem.Instance != null && ObjectiveSystem.Instance.Complete;

        public string Prompt
        {
            get
            {
                if (finished) return "Listo";
                if (!HasEverything)
                {
                    int left = ObjectiveSystem.Instance != null ? ObjectiveSystem.Instance.Remaining : 0;
                    // Naming what is missing rather than refusing silently: the bench is a
                    // landmark the player will walk past early, and it should teach them what it
                    // is for the first time they see it.
                    return $"Faltan {left} componentes";
                }
                return "Armar la computadora";
            }
        }

        public bool CanInteract(GameObject interactor) => !finished && HasEverything && isActiveAndEnabled;

        public void Interact(GameObject interactor) { }

        private void Update()
        {
            if (finished || progress <= 0f) return;

            idleSeconds += Time.deltaTime;
            if (idleSeconds >= patienceSeconds)
                progress = Mathf.Max(0f, progress - Time.deltaTime * 0.5f);
        }

        /// <returns>True while there is still work to do.</returns>
        public bool Advance(float deltaSeconds)
        {
            if (finished || !HasEverything) return false;

            idleSeconds = 0f;
            progress += deltaSeconds;

            noiseCooldown -= deltaSeconds;
            if (noiseCooldown <= 0f)
            {
                noiseCooldown = 0.7f;
                NoiseSystem.Emit(transform.position, noiseRadius, NoiseSource.Minigame);
            }

            GameSignals.RaiseTheftProgressChanged(Progress01, "Armando la computadora");

            if (progress < assembleSeconds) return true;

            finished = true;
            GameSignals.RaiseTheftProgressChanged(-1f, null);
            GameSignals.RaiseRunWon();
            return false;
        }
    }
}
