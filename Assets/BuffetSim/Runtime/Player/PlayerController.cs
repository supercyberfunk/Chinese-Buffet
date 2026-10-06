using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>First-person movement and mouse look on a CharacterController. Nothing else.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 0.8f;
        [SerializeField] private Transform cameraPivot;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private bool _cursorLocked;

        public Transform CameraPivot
        {
            get => cameraPivot;
            set => cameraPivot = value;
        }

        public bool CursorLocked => _cursorLocked;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            if (InputReader.EscapePressed()) SetCursorLocked(false);
            else if (!_cursorLocked && InputReader.ClickPressed()) SetCursorLocked(true);

            if (_cursorLocked) HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            Vector2 look = InputReader.Look() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            Vector2 input = _cursorLocked ? InputReader.Move() : Vector2.zero;
            float speed = InputReader.SprintHeld() ? sprintSpeed : walkSpeed;
            Vector3 planar = (transform.right * input.x + transform.forward * input.y) * speed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f;
                if (_cursorLocked && InputReader.JumpPressed())
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move((planar + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        private void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
