using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>The little green thief's interactable surface: one punch and the runner takes over.</summary>
    public sealed class Leprechaun : MonoBehaviour, IInteractable
    {
        private LeprechaunRunner _owner;
        private bool _down;

        public bool IsDown => _down;

        public void Initialize(LeprechaunRunner owner)
        {
            _owner = owner;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            return _down ? "The leprechaun is out cold" : "[E] Knock out the leprechaun";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_down) return;
            _down = true;
            if (_owner != null) _owner.OnDecked();
        }
    }
}
