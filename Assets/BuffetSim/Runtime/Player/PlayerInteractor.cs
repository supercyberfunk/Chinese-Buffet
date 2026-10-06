using BuffetSim.Core;
using BuffetSim.Interaction;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// Raycasts from the camera for <see cref="IInteractable"/>s, publishes the prompt, and
    /// forwards the interact/drop keys. It never mutates trays, tables or money itself.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float reach = 3f;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerInventory inventory;

        private IInteractable _current;
        private string _lastPrompt;

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

        private void Update()
        {
            if (viewCamera == null || inventory == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                PublishPrompt(string.Empty);
                return;
            }

            _current = FindTarget();
            PublishPrompt(_current != null ? _current.GetPrompt(inventory) : string.Empty);

            if (_current != null && InputReader.InteractPressed())
                _current.Interact(inventory);

            if (InputReader.DropPressed() && !inventory.HandsFree)
                DropHeldItems();
        }

        private IInteractable FindTarget()
        {
            Transform cam = viewCamera.transform;
            var ray = new Ray(cam.position, cam.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                return null;
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        private void DropHeldItems()
        {
            if (inventory.IsHoldingPlates)
            {
                // Dropping a stack of plates on the floor breaks them; the economy applies the penalty.
                int plates = inventory.HeldPlates;
                inventory.DropEverything();
                GameEvents.RaiseDishesBroken(plates, transform.position);
                return;
            }

            if (inventory.IsHoldingFood)
            {
                GameEvents.RaiseNotice($"Dropped the tray of {inventory.HeldFood.DisplayName} ({inventory.HeldFoodUnits} units lost).");
                inventory.DropEverything();
            }
        }

        private void PublishPrompt(string prompt)
        {
            if (prompt == _lastPrompt) return;
            _lastPrompt = prompt;
            GameEvents.RaisePromptChanged(prompt);
        }
    }
}
