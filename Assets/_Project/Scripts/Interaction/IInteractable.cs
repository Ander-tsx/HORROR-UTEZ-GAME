using UnityEngine;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// Anything the player can point at and act on. Implemented by doors, hiding spots,
    /// the generator, the charge point, and every step of a component theft.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Verb shown in the HUD, e.g. "Unscrew panel".</summary>
        string Prompt { get; }

        bool CanInteract(GameObject interactor);

        void Interact(GameObject interactor);
    }
}
