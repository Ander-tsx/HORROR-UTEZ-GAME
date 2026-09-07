using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UtezHorror.Controls;
using UtezHorror.Interaction;

namespace UtezHorror.Player
{
    /// <summary>
    /// The one place that talks to the input asset. Everything else on the player reads
    /// values or subscribes to events here, so adding touch controls or remapping a key
    /// never touches gameplay code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;

        private PlayerControls controls;
        private bool gameplayEnabled = true;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }

        /// <summary>
        /// True when Look is being driven by a mouse or a touchscreen. Those report a
        /// per-frame delta that is already framerate-independent; a stick reports a rate
        /// and must be multiplied by deltaTime. Getting this wrong makes aiming speed
        /// scale with framerate on gamepad and phone.
        /// </summary>
        public bool LookIsPointerDelta { get; private set; } = true;

        public bool SprintHeld { get; private set; }

        /// <summary>Held, not toggled: crouching has to cost you something to keep doing.</summary>
        public bool CrouchHeld { get; private set; }

        /// <summary>
        /// Interact held down, alongside the press event.
        ///
        /// A door is a press; taking a component is work you hold. Both come off the same action
        /// so there is nothing new to bind and nothing new to explain on a phone.
        /// </summary>
        public bool InteractHeld { get; private set; }

        public event Action InteractPressed;
        public event Action CancelPressed;
        public event Action FlashlightPressed;
        public event Action ObjectivesPressed;
        public event Action PausePressed;

        private void Awake()
        {
            controls = new PlayerControls();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
        }

        private void OnEnable()
        {
            controls.Player.Enable();

            controls.Player.Interact.performed += OnInteract;
            controls.Player.Cancel.performed += OnCancel;
            controls.Player.Flashlight.performed += OnFlashlight;
            controls.Player.Objectives.performed += OnObjectives;
            controls.Player.Pause.performed += OnPause;

            SetGameplayInputEnabled(true);
        }

        private void OnDisable()
        {
            controls.Player.Interact.performed -= OnInteract;
            controls.Player.Cancel.performed -= OnCancel;
            controls.Player.Flashlight.performed -= OnFlashlight;
            controls.Player.Objectives.performed -= OnObjectives;
            controls.Player.Pause.performed -= OnPause;

            controls.Player.Disable();
            SetCursorLocked(false);
        }

        private void OnDestroy() => controls?.Dispose();

        private void Update()
        {
            if (!gameplayEnabled)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                return;
            }

            MoveInput = controls.Player.Move.ReadValue<Vector2>();
            LookInput = controls.Player.Look.ReadValue<Vector2>();
            SprintHeld = controls.Player.Sprint.IsPressed();
            CrouchHeld = controls.Player.Crouch.IsPressed();
            InteractHeld = controls.Player.Interact.IsPressed();

            // activeControl goes null while the stick rests, so only update on real input.
            InputControl active = controls.Player.Look.activeControl;
            if (active != null) LookIsPointerDelta = active.device is Pointer;
        }

        /// <summary>Cuts movement and look without disabling Pause, so menus still work.</summary>
        public void SetGameplayInputEnabled(bool enabled)
        {
            gameplayEnabled = enabled;
            SetCursorLocked(enabled);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!gameplayEnabled) return;
            if (interactor != null && interactor.TryInteract()) return;
            InteractPressed?.Invoke();
        }

        private void OnCancel(InputAction.CallbackContext _) => CancelPressed?.Invoke();
        private void OnFlashlight(InputAction.CallbackContext _) { if (gameplayEnabled) FlashlightPressed?.Invoke(); }
        private void OnObjectives(InputAction.CallbackContext _) => ObjectivesPressed?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
