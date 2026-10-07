using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// First-person movement and mouse look on a CharacterController. Reads (never writes) the
    /// player's effects and load: fortunes change the speeds, a glass pane in your hands means no
    /// sprinting, and falls tilt the camera. Holding E on something keeps you in place.
    /// </summary>
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
        private PlayerEffects _effects;
        private PlayerInventory _inventory;
        private float _pitch;
        private float _verticalVelocity;
        private bool _cursorLocked;
        private Vector3 _pivotRest;
        private bool _pivotRestKnown;
        private float _planarSpeed;

        public Transform CameraPivot
        {
            get => cameraPivot;
            set
            {
                cameraPivot = value;
                _pivotRestKnown = false;
            }
        }

        public bool CursorLocked => _cursorLocked;

        /// <summary>Set by the interactor while E is held on something: look around, but stay put.</summary>
        public bool MovementLocked { get; set; }

        /// <summary>True while the player is walking or running (standing still lights cigarettes).</summary>
        public bool IsMoving => _planarSpeed > 0.15f;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            _effects = GetComponent<PlayerEffects>();
            _inventory = GetComponent<PlayerInventory>();
            SetCursorLocked(true);
        }

        private void Update()
        {
            if (InputReader.EscapePressed()) SetCursorLocked(false);
            else if (!_cursorLocked && InputReader.ClickPressed()) SetCursorLocked(true);

            if (_cursorLocked) HandleLook();
            ApplyCameraPivot();
            HandleMove();
        }

        /// <summary>Moves the player instantly (spawns, events). Safe with the CharacterController enabled.</summary>
        public void Teleport(Vector3 position)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = wasEnabled;
            _verticalVelocity = 0f;
        }

        private void HandleLook()
        {
            Vector2 look = InputReader.Look() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
        }

        private void ApplyCameraPivot()
        {
            if (cameraPivot == null) return;
            if (!_pivotRestKnown)
            {
                _pivotRest = cameraPivot.localPosition;
                _pivotRestKnown = true;
            }
            float roll = _effects != null ? _effects.ViewRoll : 0f;
            float dip = _effects != null ? _effects.ViewDip : 0f;
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, roll);
            cameraPivot.localPosition = _pivotRest + Vector3.down * dip;
        }

        private void HandleMove()
        {
            _planarSpeed = 0f;
            // Aliens and teleports switch the controller off; Move on a disabled controller only logs a warning.
            if (!_controller.enabled) return;

            bool locked = MovementLocked || (_effects != null && _effects.ControlsLocked);
            Vector2 input = _cursorLocked && !locked ? InputReader.Move() : Vector2.zero;

            bool sprintAllowed = (_effects == null || _effects.SprintAllowed) && (_inventory == null || !_inventory.HeldItemBlocksSprint);
            bool sprinting = sprintAllowed && InputReader.SprintHeld();
            float speed = sprinting ? sprintSpeed * (_effects != null ? _effects.SprintMultiplier : 1f) : walkSpeed;
            if (_effects != null) speed *= _effects.SpeedMultiplier;

            Vector3 planar = (transform.right * input.x + transform.forward * input.y) * speed;
            _planarSpeed = planar.magnitude;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f;
                if (_cursorLocked && !locked && InputReader.JumpPressed())
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
