using System.Text;
using BuffetSim.Core;
using BuffetSim.Interaction;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// Raycasts from the camera for <see cref="IInteractable"/>s, publishes the prompt, and forwards
    /// the keys: E presses (or holds, for <see cref="IHoldInteractable"/>s, with a progress bar and
    /// the player kept in place), Q for a target's secondary action or to drop what you carry. It
    /// never mutates trays, tables or money itself.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int BarSegments = 24;

        [SerializeField] private float reach = 3f;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerInventory inventory;

        private PlayerController _controller;
        private PlayerEffects _effects;
        private IInteractable _current;
        private string _lastPrompt;

        // Channelling a hold.
        private IHoldInteractable _holdTarget;
        private float _holdSeconds;
        private float _holdProgress;

        public Camera ViewCamera
        {
            get => viewCamera;
            set => viewCamera = value;
        }

        public PlayerInventory Inventory
        {
            get => inventory;
            set => inventory = value;
        }

        public float Reach => reach;

        /// <summary>True while E is being held on something.</summary>
        public bool IsChannelling => _holdTarget != null;

        private void Start()
        {
            _controller = GetComponent<PlayerController>();
            _effects = GetComponent<PlayerEffects>();
        }

        private void Update()
        {
            if (viewCamera == null || inventory == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                CancelHold();
                PublishPrompt(string.Empty);
                return;
            }

            if (_effects != null && _effects.ControlsLocked)
            {
                CancelHold();
                PublishPrompt(_effects.IsAbducted ? "You are being abducted." : _effects.IsKnockedDown ? "You are out cold." : string.Empty);
                return;
            }

            _current = FindTarget();

            if (_holdTarget != null)
            {
                TickHold();
                return;
            }

            string prompt = _current != null ? _current.GetPrompt(inventory) : string.Empty;
            string secondary = SecondaryPrompt(_current);
            PublishPrompt(string.IsNullOrEmpty(secondary) ? prompt : (string.IsNullOrEmpty(prompt) ? secondary : $"{prompt}\n{secondary}"));

            if (_current != null && InputReader.InteractPressed())
            {
                if (_current is IHoldInteractable hold && hold.GetHoldSeconds(inventory) > 0f)
                    StartHold(hold);
                else
                    _current.Interact(inventory);
            }

            if (InputReader.DropPressed())
            {
                if (!string.IsNullOrEmpty(secondary) && _current is ISecondaryInteractable second)
                    second.SecondaryInteract(inventory);
                else if (!inventory.HandsFree)
                    inventory.SpillLoad(transform.position, "Dropped");
            }
        }

        private void StartHold(IHoldInteractable target)
        {
            _holdTarget = target;
            _holdSeconds = Mathf.Max(0.1f, target.GetHoldSeconds(inventory));
            _holdProgress = 0f;
            if (_controller != null) _controller.MovementLocked = true;
            TickHold();
        }

        /// <summary>Keep holding E, keep looking at it: the bar fills. Let go or look away and it resets.</summary>
        private void TickHold()
        {
            if (!InputReader.InteractHeld() || !ReferenceEquals(_current, _holdTarget) || IsDestroyed(_holdTarget))
            {
                CancelHold();
                return;
            }

            // The target can change its mind (the job got done another way, the customer left).
            if (_holdTarget.GetHoldSeconds(inventory) <= 0f)
            {
                CancelHold();
                return;
            }

            _holdProgress += Time.deltaTime;
            if (_holdProgress >= _holdSeconds)
            {
                IHoldInteractable done = _holdTarget;
                CancelHold();
                done.CompleteHold(inventory);
                // Still holding, still looking, still something to do (the fountain): the next bar starts at once.
                if (InputReader.InteractHeld() && ReferenceEquals(_current, done) && !IsDestroyed(done) && done.GetHoldSeconds(inventory) > 0f)
                    StartHold(done);
                return;
            }

            PublishPrompt($"{_holdTarget.GetPrompt(inventory)}\n{Bar(_holdProgress / _holdSeconds)}  {Mathf.Max(0f, _holdSeconds - _holdProgress):0.0}s");
        }

        private void CancelHold()
        {
            if (_holdTarget == null) return;
            _holdTarget = null;
            _holdProgress = 0f;
            if (_controller != null) _controller.MovementLocked = false;
        }

        private string SecondaryPrompt(IInteractable target)
        {
            if (!(target is ISecondaryInteractable second)) return string.Empty;
            string text = second.GetSecondaryPrompt(inventory);
            return string.IsNullOrEmpty(text) ? string.Empty : text;
        }

        private IInteractable FindTarget()
        {
            Transform cam = viewCamera.transform;
            var ray = new Ray(cam.position, cam.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                return null;
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        private static bool IsDestroyed(object target)
        {
            return target is Object unityObject && unityObject == null;
        }

        private static string Bar(float fraction)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * BarSegments), 0, BarSegments);
            var bar = new StringBuilder("<color=#FFD24A>");
            bar.Append('|', filled);
            bar.Append("</color><color=#666666>");
            bar.Append('|', BarSegments - filled);
            bar.Append("</color>");
            return bar.ToString();
        }

        private void PublishPrompt(string prompt)
        {
            if (prompt == _lastPrompt) return;
            _lastPrompt = prompt;
            GameEvents.RaisePromptChanged(prompt);
        }
    }
}
