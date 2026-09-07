using UnityEngine;
using UtezHorror.Core.Noise;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// A swinging door. Handles single leaves and double (glass) doors; a double door's
    /// second leaf swings the opposite way so the pair opens outward from the centre.
    ///
    /// Opening makes noise on purpose — moving between rooms should never be free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        [Header("Leaves")]
        [Tooltip("Hinge transforms. Leaf 0 swings positive, leaf 1 swings negative.")]
        [SerializeField] private Transform[] leaves;

        [SerializeField] private float openAngle = 95f;
        [SerializeField, Min(0.05f)] private float openSeconds = 0.5f;

        [Header("State")]
        [SerializeField] private bool locked;
        [SerializeField] private bool startOpen;

        [Header("Prompts")]
        [SerializeField] private string openPrompt = "Abrir";
        [SerializeField] private string closePrompt = "Cerrar";
        [SerializeField] private string lockedPrompt = "Cerrado";

        [Header("Noise")]
        [Tooltip("Hearing radius of opening or closing this door, in metres.")]
        [SerializeField, Min(0f)] private float noiseRadius = 9f;

        private Quaternion[] closedRotations;
        private float openness;      // 0 = shut, 1 = fully open
        private float target;

        public bool IsOpen => target > 0.5f;
        public bool IsLocked => locked;

        public string Prompt => locked ? lockedPrompt : (IsOpen ? closePrompt : openPrompt);

        private void Awake()
        {
            if (leaves == null || leaves.Length == 0)
            {
                Debug.LogError($"[Door] '{name}' has no leaves assigned.", this);
                enabled = false;
                return;
            }

            closedRotations = new Quaternion[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
                closedRotations[i] = leaves[i].localRotation;

            if (startOpen && !locked)
            {
                openness = 1f;
                target = 1f;
            }

            ApplyRotation();
        }

        // A locked door still shows its prompt ("Cerrado"), so the player learns the room
        // exists and is denied — silence would read as a missing interactable.
        public bool CanInteract(GameObject interactor) => isActiveAndEnabled;

        public void Interact(GameObject interactor)
        {
            if (locked)
            {
                NoiseSystem.Emit(transform.position, noiseRadius * 0.4f, NoiseSource.Interaction);
                return;
            }

            target = IsOpen ? 0f : 1f;
            NoiseSystem.Emit(transform.position, noiseRadius, NoiseSource.Interaction);
        }

        public void SetLocked(bool value) => locked = value;

        private void Update()
        {
            if (Mathf.Approximately(openness, target)) return;

            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / openSeconds);
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            // Smoothstep keeps the swing from starting and stopping abruptly.
            float eased = openness * openness * (3f - 2f * openness);
            for (int i = 0; i < leaves.Length; i++)
            {
                float sign = i % 2 == 0 ? 1f : -1f;
                leaves[i].localRotation =
                    closedRotations[i] * Quaternion.Euler(0f, openAngle * eased * sign, 0f);
            }
        }
    }
}
