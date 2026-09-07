using UnityEngine;
using UnityEngine.Events;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// Greybox-friendly interactable: wire the response in the inspector. Real systems
    /// (theft minigame, generator) implement <see cref="IInteractable"/> directly instead.
    /// </summary>
    public class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Interact";
        [SerializeField] private bool singleUse;
        [SerializeField] private UnityEvent<GameObject> onInteract;

        private bool consumed;

        public string Prompt => prompt;

        public virtual bool CanInteract(GameObject interactor) => !consumed && isActiveAndEnabled;

        public virtual void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (singleUse) consumed = true;
            onInteract?.Invoke(interactor);
        }

        protected void ResetConsumed() => consumed = false;
    }
}
