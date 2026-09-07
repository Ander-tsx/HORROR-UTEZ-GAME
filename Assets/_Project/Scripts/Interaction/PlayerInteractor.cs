using System;
using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Utils;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// Finds what the player is looking at. Holds no input of its own — the player's input
    /// router calls <see cref="TryInteract"/> — so the interaction layer stays independent
    /// of the control scheme, which is what lets touch and keyboard share it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform rayOrigin;
        [SerializeField, Min(0.1f)] private float range = 2.5f;

        [Tooltip("Seconds between target scans. Interaction does not need to run every frame.")]
        [SerializeField, Min(0f)] private float scanInterval = 0.1f;

        private readonly RaycastHit[] hits = new RaycastHit[4];
        private Collider cachedCollider;
        private IInteractable current;
        private float nextScanTime;

        /// <summary>Raised with the new target (or null) whenever the aim changes.</summary>
        public event Action<IInteractable> TargetChanged;

        public IInteractable Current => current;

        private void Awake()
        {
            if (rayOrigin == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                rayOrigin = cam != null ? cam.transform : transform;
            }
        }

        private void OnDisable() => SetCurrent(null);

        private void Update()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + scanInterval;
            Scan();
        }

        private void Scan()
        {
            int count = Physics.RaycastNonAlloc(rayOrigin.position, rayOrigin.forward, hits, range,
                                                GameLayers.InteractionMask, QueryTriggerInteraction.Collide);
            if (count == 0)
            {
                SetCurrent(null);
                return;
            }

            Collider closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= closestDistance) continue;
                closestDistance = hits[i].distance;
                closest = hits[i].collider;
            }

            if (closest == cachedCollider) return;   // same object, keep the resolved interface
            cachedCollider = closest;

            // Colliders often sit on children of the object carrying the behaviour.
            IInteractable found = closest != null ? closest.GetComponentInParent<IInteractable>() : null;
            SetCurrent(found != null && found.CanInteract(gameObject) ? found : null);
        }

        private void SetCurrent(IInteractable next)
        {
            if (ReferenceEquals(current, next)) return;
            current = next;
            TargetChanged?.Invoke(current);

            // Also on the bus, so the HUD can show the verb without the UI assembly having to
            // reference the interaction system.
            GameSignals.RaiseInteractionPromptChanged(current?.Prompt);
        }

        /// <summary>Returns true if something was actually interacted with.</summary>
        public bool TryInteract()
        {
            if (current == null || !current.CanInteract(gameObject)) return false;
            current.Interact(gameObject);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = rayOrigin != null ? rayOrigin : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin.position, origin.position + origin.forward * range);
        }
    }
}
