using System.Collections;
using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.Player
{
    /// <summary>
    /// The death sequence: control is taken away, the camera is turned to face whatever killed
    /// you, the world shakes, and the run ends.
    ///
    /// It is a scripted camera move rather than an animation because there is no rigged
    /// character to animate yet — and honestly, a forced look at the thing that caught you does
    /// more work than any death animation would. Being made to watch is the point.
    ///
    /// Control is taken by disabling the movement component rather than by an "isDead" branch
    /// inside it. A frozen player is a component that is off, not a flag that every method has
    /// to remember to check.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlayerDeath : MonoBehaviour
    {
        [Tooltip("How many catches the run survives. 1 means the first one kills you.\n\n" +
                 "This is the open question in Docs/Plans/01 expressed as a number: the GDD says " +
                 "total defeat, the recommendation is a price plus three strikes. Raise it to 3 " +
                 "and the strikes rule exists, with no code change.")]
        [SerializeField, Min(1)] private int capturesAllowed = 1;

        [Tooltip("Seconds the death sequence runs before the loss screen appears.")]
        [SerializeField, Min(0.2f)] private float sequenceSeconds = 2.2f;

        [Tooltip("Seconds of immunity after surviving a capture.")]
        [SerializeField, Min(0f)] private float graceSeconds = 3f;

        [SerializeField] private float shakeAmplitude = 0.09f;

        private FirstPersonController movement;
        private PlayerFlashlight torch;
        private CharacterController controller;
        private Camera view;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private float immuneUntil;
        private bool dying;

        /// <summary>Catches so far this run.</summary>
        public int Captures { get; private set; }

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            torch = GetComponent<PlayerFlashlight>();
            controller = GetComponent<CharacterController>();
            view = GetComponentInChildren<Camera>();

            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void OnEnable() => GameSignals.EnemyCaughtPlayer += OnCaught;

        private void OnDisable() => GameSignals.EnemyCaughtPlayer -= OnCaught;

        private void OnCaught(Vector3 where)
        {
            if (dying || Time.time < immuneUntil) return;

            Captures++;

            if (Captures >= capturesAllowed) StartCoroutine(Die(where));
            else Survive();
        }

        /// <summary>Only reachable when capturesAllowed is raised above one.</summary>
        private void Survive()
        {
            immuneUntil = Time.time + graceSeconds;

            // Losing the torch is the part that hurts: you are put back at the entrance in the
            // dark, and switching it on again spends battery you were saving.
            if (torch != null && torch.IsOn) torch.Toggle();
            Teleport(startPosition, startRotation);
        }

        private IEnumerator Die(Vector3 killerPosition)
        {
            dying = true;

            // Off, not flagged: nothing downstream has to remember that the player is dead.
            if (movement != null) movement.enabled = false;
            if (torch != null && torch.IsOn) torch.Toggle();

            GameSignals.RaisePlayerDying(killerPosition);

            Quaternion from = view != null ? view.transform.rotation : transform.rotation;
            Vector3 toKiller = killerPosition + Vector3.up * 1.5f
                               - (view != null ? view.transform.position : transform.position);
            Quaternion to = toKiller.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toKiller)
                : from;

            float elapsed = 0f;
            Vector3 eyeRest = view != null ? view.transform.localPosition : Vector3.zero;

            while (elapsed < sequenceSeconds)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / sequenceSeconds;

                if (view != null)
                {
                    // Turn to face it fast, then sag: the head is being pulled round and then
                    // dropping. The sag is the part that reads as dying rather than as a cutscene.
                    view.transform.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t * 3f));
                    view.transform.rotation *= Quaternion.Euler(t * t * 34f, 0f, t * t * 18f);

                    // Shake fades out rather than in, so the hardest jolt is the moment of impact.
                    float shake = shakeAmplitude * (1f - t);
                    view.transform.localPosition = eyeRest + new Vector3(
                        Random.Range(-shake, shake), Random.Range(-shake, shake), 0f);
                }

                yield return null;
            }

            GameSignals.RaiseRunLost(RunOutcome.Caught);
        }

        /// <summary>
        /// A CharacterController overwrites transform moves with its own internal position, so
        /// setting transform.position on an enabled one silently does nothing. Disabling it
        /// across the move is the supported way to teleport.
        /// </summary>
        private void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = wasEnabled;
        }
    }
}
