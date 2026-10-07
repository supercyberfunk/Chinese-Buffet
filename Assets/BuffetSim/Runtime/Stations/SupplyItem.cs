using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    /// <summary>
    /// One slot on the maintenance shelf: lightbulbs, a roll of duct tape, the wrench, a glass pane.
    /// Empty-handed, E takes one (paid from the till when it has a price, the moment you lift it);
    /// holding the same thing, E puts it back. A daily stock refills every morning; -1 means endless.
    /// </summary>
    public sealed class SupplyItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private float cost;
        [SerializeField] private int usesPerItem = 1;
        [SerializeField] private int dailyStock = -1;
        [SerializeField] private bool blocksSprint;
        [SerializeField] private bool fragile;
        [SerializeField] private TextMesh label;

        private int _stock;

        public string ItemId => itemId;
        public int Stock => _stock;

        // The display name is a bare noun ("lightbulb", "duct tape"); the prompts add the article. A fragile
        // part is one of several you take ("a lightbulb", "a glass pane"); the rest are the shelf's own
        // tools you borrow ("the wrench", "the duct tape").
        private string Article => fragile ? "a" : "the";
        private string Capitalized => string.IsNullOrEmpty(displayName) ? displayName : char.ToUpperInvariant(displayName[0]) + displayName.Substring(1);

        public void Configure(string id, string name, float price, int uses, int stockPerDay, bool heavy, bool breaks, TextMesh stockLabel)
        {
            itemId = id;
            displayName = name;
            cost = price;
            usesPerItem = Mathf.Max(1, uses);
            dailyStock = stockPerDay;
            blocksSprint = heavy;
            fragile = breaks;
            label = stockLabel;
            _stock = dailyStock;
            RefreshLabel();
        }

        private void OnEnable()
        {
            GameEvents.DayStarted += OnDayStarted;
        }

        private void OnDisable()
        {
            GameEvents.DayStarted -= OnDayStarted;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (inventory == null) return Capitalized;
            if (inventory.IsHolding(itemId)) return $"[E] Put the {displayName} back";
            if (!inventory.HandsFree) return $"{Capitalized} - hands full";
            if (dailyStock >= 0 && _stock <= 0) return $"{Capitalized} - none left today";
            string price = cost > 0f ? $" (${cost:0.00} from the till)" : string.Empty;
            string extra = blocksSprint ? ", two hands, no sprinting" : usesPerItem > 1 ? $", {usesPerItem} uses" : string.Empty;
            return $"[E] Take {Article} {displayName}{price}{extra}";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (inventory == null) return;

            if (inventory.IsHolding(itemId))
            {
                // An untouched paid item goes back on the till over the bus; a partly used one is yours now.
                bool unused = inventory.HeldItemUses == usesPerItem;
                bool refunded = cost > 0f && unused;
                if (refunded) GameEvents.RaiseMoneyRecovered(cost, $"{displayName} returned", transform.position);
                inventory.ClearItem();
                if (dailyStock >= 0) _stock++;
                GameEvents.RaiseNotice(refunded
                    ? $"Put the {displayName} back on the shelf. ${cost:0.00} back in the till."
                    : cost > 0f
                        ? $"Put the part-used {displayName} back on the shelf. No refund for an opened one."
                        : $"Put the {displayName} back on the shelf.");
                RefreshLabel();
                return;
            }

            if (!inventory.HandsFree) return;
            if (dailyStock >= 0 && _stock <= 0)
            {
                GameEvents.RaiseNotice($"The {displayName} box is empty. It refills overnight.");
                return;
            }

            if (cost > 0f)
            {
                var request = new PurchaseRequest(cost, $"{Article} {displayName}", transform.position);
                GameEvents.RaisePurchaseRequested(request);
                if (!request.Approved) return;
            }

            if (!inventory.TryTakeItem(itemId, displayName, usesPerItem, blocksSprint, fragile)) return;
            if (dailyStock >= 0) _stock--;
            GameEvents.RaiseNotice(blocksSprint
                ? $"Took {Article} {displayName}. It's awkward. Walk, don't run."
                : fragile ? $"Took {Article} {displayName}. Don't drop it." : $"Took {Article} {displayName}.");
            RefreshLabel();
        }

        private void OnDayStarted(int day)
        {
            if (dailyStock >= 0) _stock = dailyStock;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (label == null) return;
            string stock = dailyStock >= 0 ? $"\n{_stock} left" : string.Empty;
            string price = cost > 0f ? $"\n${cost:0}" : string.Empty;
            label.text = $"{Capitalized}{stock}{price}";
        }
    }
}
