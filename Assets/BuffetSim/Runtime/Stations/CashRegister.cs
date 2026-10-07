using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    /// <summary>The till. It mostly just sits there; a few event items (the dragon tarp) sell here.</summary>
    public sealed class CashRegister : MonoBehaviour, IInteractable
    {
        [SerializeField] private float tarpPrice = 20f;

        private float _till;

        private void OnEnable()
        {
            GameEvents.LedgerUpdated += OnLedgerUpdated;
        }

        private void OnDisable()
        {
            GameEvents.LedgerUpdated -= OnLedgerUpdated;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (inventory.IsHolding(CarryItems.Tarp)) return $"[E] Sell the dragon tarp (${tarpPrice:0.00})";
            return $"Cash register: ${_till:0.00} in the till";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (!inventory.IsHolding(CarryItems.Tarp)) return;
            inventory.ClearItem();
            GameEvents.RaiseMoneyRecovered(tarpPrice, "a parade dragon tarp", transform.position + Vector3.up);
            GameEvents.RaiseNotice($"Sold the dragon tarp for ${tarpPrice:0.00}. A guy from the parade will be by to not ask about it.");
        }

        private void OnLedgerUpdated(LedgerSnapshot snapshot)
        {
            _till = snapshot.Balance;
        }
    }
}
