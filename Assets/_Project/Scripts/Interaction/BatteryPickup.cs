using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Core.Noise;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// A spare battery lying somewhere in the building. Picking it up refills part of the phone
    /// torch and removes it from the world for good.
    ///
    /// The torch battery is the run's only consumable, so these are the only thing standing
    /// between the player and total darkness. That is why they are deliberately scarce: make
    /// them common and the resource stops mattering, which removes the pressure that sends the
    /// player back out into the corridors in the first place.
    ///
    /// It talks to the torch through <see cref="GameSignals"/> rather than by finding the
    /// component. Not ceremony: this class lives in the Interaction assembly and the torch in
    /// Player, which already depends on Interaction — a direct reference would be a cycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BatteryPickup : MonoBehaviour, IInteractable
    {
        [Tooltip("Fraction of a full charge this restores. 0.35 is a little over a minute of light.")]
        [SerializeField, Range(0.05f, 1f)] private float charge = 0.35f;

        [SerializeField] private string prompt = "Tomar batería";

        [Tooltip("Hearing radius of rummaging for it. Nothing in this building should be free.")]
        [SerializeField, Min(0f)] private float noiseRadius = 3f;

        private bool taken;

        public string Prompt => prompt;

        /// <summary>Fraction of a full charge this pickup carries.</summary>
        public float Charge => charge;

        public bool CanInteract(GameObject interactor) => !taken && isActiveAndEnabled;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            // Flagged before the signal goes out: a handler that somehow re-enters would
            // otherwise be able to claim the same battery twice.
            taken = true;

            GameSignals.RaiseBatteryPickedUp(charge);
            NoiseSystem.Emit(transform.position, noiseRadius, NoiseSource.Interaction);

            // The prompt is stale the moment this object stops existing, and the interactor only
            // rescans when the aim changes — without this the verb hangs on screen.
            GameSignals.RaiseInteractionPromptChanged(null);

            Destroy(gameObject);
        }
    }
}
