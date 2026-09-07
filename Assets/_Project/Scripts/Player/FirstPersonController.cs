using System;
using UnityEngine;
using UtezHorror.Core.Noise;

namespace UtezHorror.Player
{
    /// <summary>How the player is carrying themselves. Drives speed, noise and how visible they are.</summary>
    public enum Stance
    {
        Crouching,
        Walking,
        Sprinting
    }

    /// <summary>
    /// Walk, sprint, crouch and look, with the head movement that sells all three.
    ///
    /// Movement also emits footstep noise, which is the only thing tying player haste to enemy
    /// attention — see <see cref="NoiseSystem"/>. Crouching is the other half of that bargain:
    /// it makes you quiet and hard to see, and it costs you speed, which in a game with a clock
    /// is the most expensive currency there is.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputRouter))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(0f)] private float sprintSpeed = 5.6f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.4f;
        [SerializeField] private float gravity = -18f;

        [Header("Stance")]
        [SerializeField, Min(0.5f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.5f)] private float crouchingHeight = 1.05f;

        [Tooltip("Seconds to go from standing to crouched. Instant reads as a glitch.")]
        [SerializeField, Min(0.01f)] private float stanceBlendSeconds = 0.18f;

        [Header("Look")]
        [Tooltip("Degrees of rotation per unit of pointer delta.")]
        [SerializeField, Min(0f)] private float pointerSensitivity = 0.12f;

        [Tooltip("Degrees of rotation per second at full stick deflection.")]
        [SerializeField, Min(0f)] private float stickSensitivity = 180f;

        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Head movement")]
        [Tooltip("Vertical travel of the head at a walk, in metres.")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.045f;

        [Tooltip("Head bobs per metre walked. Tied to distance, not time, so it never desyncs.")]
        [SerializeField, Min(0f)] private float bobsPerMetre = 0.55f;

        [Tooltip("Sideways sway as a fraction of the vertical bob. A little sells a gait.")]
        [SerializeField, Range(0f, 1f)] private float swayRatio = 0.6f;

        [Tooltip("Extra roll while sprinting, in degrees.")]
        [SerializeField, Range(0f, 4f)] private float sprintRoll = 1.2f;

        [Header("Footstep noise")]
        [Tooltip("Metres travelled between footsteps.")]
        [SerializeField, Min(0.1f)] private float strideLength = 1.9f;

        [Tooltip("Hearing radius of a walking footstep, in metres.")]
        [SerializeField, Min(0f)] private float walkNoiseRadius = 4f;

        [Tooltip("Hearing radius of a sprinting footstep, in metres.")]
        [SerializeField, Min(0f)] private float sprintNoiseRadius = 13f;

        [Tooltip("Hearing radius of a crouched footstep. Nearly silent, on purpose.")]
        [SerializeField, Min(0f)] private float crouchNoiseRadius = 1.2f;

        private CharacterController controller;
        private PlayerInputRouter input;
        private PlayerInventory inventory;
        private float pitch;
        private float verticalVelocity;
        private float strideAccumulator;
        private float bobPhase;

        private float standingEyeHeight;
        private float crouch01;      // 0 standing, 1 fully crouched
        private Vector3 cameraRestPosition;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching => crouch01 > 0.5f;

        /// <summary>0 standing, 1 crouched. Continuous, so visibility can blend with it.</summary>
        public float Crouch01 => crouch01;

        public Stance Stance => IsCrouching ? Stance.Crouching
                             : IsSprinting ? Stance.Sprinting
                             : Stance.Walking;

        /// <summary>True while actually travelling, not merely holding a direction against a wall.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Raised on each footstep, with the stance that produced it. For audio.</summary>
        public event Action<Stance> Stepped;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputRouter>();
            inventory = GetComponent<PlayerInventory>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

            standingHeight = controller.height;
            if (playerCamera != null)
            {
                cameraRestPosition = playerCamera.transform.localPosition;
                standingEyeHeight = cameraRestPosition.y;
            }
        }

        private void Update()
        {
            ApplyLook();
            ApplyStance();
            ApplyMove();
            ApplyHeadMovement();
        }

        private void ApplyLook()
        {
            Vector2 look = input.LookInput;

            // A mouse/touch delta is already "amount moved this frame"; a stick is a rate.
            float scale = input.LookIsPointerDelta
                ? pointerSensitivity
                : stickSensitivity * Time.deltaTime;

            transform.Rotate(Vector3.up, look.x * scale, Space.Self);

            pitch = Mathf.Clamp(pitch - look.y * scale, minPitch, maxPitch);
        }

        /// <summary>
        /// Blends the capsule and the eye height between standing and crouched.
        ///
        /// Standing back up is refused when there is something overhead. Without that check the
        /// player pops through a desk or a stair soffit and the controller resolves the overlap
        /// by launching them, which looks exactly like a physics bug because it is one.
        /// </summary>
        private void ApplyStance()
        {
            bool wantsCrouch = input.CrouchHeld || (crouch01 > 0f && BlockedOverhead());
            float target = wantsCrouch ? 1f : 0f;

            crouch01 = Mathf.MoveTowards(crouch01, target, Time.deltaTime / stanceBlendSeconds);

            float height = Mathf.Lerp(standingHeight, crouchingHeight, crouch01);
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool BlockedOverhead()
        {
            // Cast from the crouched crown to where the standing crown would be.
            Vector3 origin = transform.position + Vector3.up * (crouchingHeight - controller.radius);
            float distance = standingHeight - crouchingHeight + 0.05f;

            return Physics.SphereCast(origin, controller.radius * 0.95f, Vector3.up, out _, distance,
                                      ~0, QueryTriggerInteraction.Ignore);
        }

        private void ApplyMove()
        {
            Vector2 move = input.MoveInput;
            bool wantsMove = move.sqrMagnitude > 0.01f;

            // Carrying a cabinet is what stops you running, not a rule in this class. See
            // PlayerInventory: bulk is the price of a component, paid over the rest of the run.
            bool canSprint = inventory == null || inventory.CanSprint;
            IsSprinting = input.SprintHeld && wantsMove && !IsCrouching && canSprint;

            Vector3 direction = transform.right * move.x + transform.forward * move.y;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            if (inventory != null) speed *= inventory.SpeedMultiplier;
            Vector3 velocity = direction * speed;

            // Small downward bias keeps isGrounded stable on slopes and stair treads.
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;

            Vector3 before = transform.position;
            controller.Move(velocity * Time.deltaTime);

            // Measured, not intended: walking into a wall should not bob the head or make noise.
            Vector3 travelled = transform.position - before;
            travelled.y = 0f;
            float distance = travelled.magnitude;
            IsMoving = wantsMove && distance > 0.0005f;

            AccumulateFootsteps(distance);
        }

        private void AccumulateFootsteps(float distance)
        {
            if (!IsMoving || !controller.isGrounded)
            {
                strideAccumulator = 0f;
                return;
            }

            // A crouched stride is shorter, so quiet movement also means slow progress twice over.
            float stride = IsCrouching ? strideLength * 0.65f : strideLength;

            strideAccumulator += distance;
            if (strideAccumulator < stride) return;

            strideAccumulator -= stride;

            float radius = IsCrouching ? crouchNoiseRadius
                         : IsSprinting ? sprintNoiseRadius
                         : walkNoiseRadius;

            NoiseSystem.Emit(transform.position, radius,
                             IsSprinting ? NoiseSource.Sprint : NoiseSource.Footstep);

            Stepped?.Invoke(Stance);
        }

        /// <summary>
        /// Eye height, head bob and sprint roll.
        ///
        /// The bob is driven by distance travelled rather than by time, so it stays locked to the
        /// footsteps at any speed and cannot drift out of phase with them — a head that bobs on
        /// a different rhythm from the footstep sounds is deeply wrong in a way players feel
        /// without being able to name.
        /// </summary>
        private void ApplyHeadMovement()
        {
            if (playerCamera == null) return;

            float eyeHeight = Mathf.Lerp(standingEyeHeight,
                                         standingEyeHeight - (standingHeight - crouchingHeight),
                                         crouch01);

            Vector3 offset = Vector3.zero;
            float roll = 0f;

            if (IsMoving)
            {
                float amplitude = bobAmplitude * (IsSprinting ? 1.7f : IsCrouching ? 0.45f : 1f);
                bobPhase += Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude
                            * bobsPerMetre * Mathf.PI * 2f * Time.deltaTime;

                // The sideways sway runs at half the vertical rate: one lateral shift per pair
                // of steps, which is what walking actually does.
                offset.y = Mathf.Sin(bobPhase) * amplitude;
                offset.x = Mathf.Sin(bobPhase * 0.5f) * amplitude * swayRatio;

                if (IsSprinting) roll = Mathf.Sin(bobPhase * 0.5f) * sprintRoll;
            }
            else
            {
                bobPhase = Mathf.MoveTowards(bobPhase % (Mathf.PI * 2f), 0f, Time.deltaTime * 6f);
            }

            Vector3 rest = cameraRestPosition;
            rest.y = eyeHeight;
            playerCamera.transform.localPosition =
                Vector3.Lerp(playerCamera.transform.localPosition, rest + offset, Time.deltaTime * 14f);

            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
