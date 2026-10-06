using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    /// <summary>
    /// Load dirty plates (up to 50), then pull the handle to run it. Clean plates restock the
    /// buffet automatically for now, per the notes.
    /// </summary>
    public sealed class Dishwasher : MonoBehaviour, IInteractable
    {
        [SerializeField] private int capacity = 50;
        [SerializeField] private TextMesh label;

        private int _loaded;
        private int _washedTotal;

        public int Loaded => _loaded;

        public void Configure(int plateCapacity, TextMesh statusLabel)
        {
            capacity = Mathf.Max(1, plateCapacity);
            label = statusLabel;
            RefreshLabel();
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (inventory.IsHoldingPlates)
            {
                return _loaded >= capacity
                    ? $"Dishwasher is full ({_loaded}/{capacity}) - run it first"
                    : $"[E] Load {inventory.HeldPlates} dirty plates ({_loaded}/{capacity})";
            }
            if (inventory.IsHoldingFood) return $"Dishwasher ({_loaded}/{capacity}) - hands full";
            return _loaded > 0 ? $"[E] Run the dishwasher ({_loaded} plates)" : "Dishwasher (empty)";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (inventory.IsHoldingPlates)
            {
                int space = capacity - _loaded;
                int loaded = inventory.RemovePlates(space);
                _loaded += loaded;
                GameEvents.RaiseNotice(loaded > 0 ? $"Loaded {loaded} plates into the dishwasher ({_loaded}/{capacity})." : "The dishwasher is full.");
                RefreshLabel();
                return;
            }

            if (inventory.HandsFree && _loaded > 0)
            {
                _washedTotal += _loaded;
                GameEvents.RaiseNotice($"Washed {_loaded} plates; clean plates restocked on the buffet.");
                _loaded = 0;
                RefreshLabel();
            }
        }

        private void RefreshLabel()
        {
            if (label != null) label.text = $"Dishwasher\n{_loaded}/{capacity}";
        }
    }
}
