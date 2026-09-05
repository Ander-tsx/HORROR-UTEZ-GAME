using UnityEngine;
using UnityEngine.InputSystem;

namespace UtezHorror.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        private CharacterController controller;
        private Vector2 moveInput;
        private Vector2 lookInput;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

        public void OnLook(InputValue value) => lookInput = value.Get<Vector2>();

        private void Update()
        {
            ApplyLook();
            ApplyMove();
        }

        private void ApplyLook()
        {
            float yaw = lookInput.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - lookInput.y * lookSensitivity, minPitch, maxPitch);

            transform.Rotate(Vector3.up * yaw);
            if (playerCamera != null)
                playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        private void ApplyMove()
        {
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            move *= moveSpeed;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);
        }
    }
}
