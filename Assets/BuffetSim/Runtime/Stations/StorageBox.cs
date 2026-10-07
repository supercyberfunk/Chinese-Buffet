using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    /// <summary>
    /// A tupperware box of one food in the storage cooler. With cooking on it holds two piles: cooked
    /// units (what the cookers sent back, plus the day-one stock) that go straight to a buffet tray,
    /// and raw boxes bought wholesale ($15 for 20 units) that have to go through a cooker first.
    /// With cooking off every box comes out ready, like the first demo.
    /// </summary>
    public sealed class StorageBox : MonoBehaviour, IInteractable, ISecondaryInteractable
    {
        [SerializeField] private FoodDefinition food;
        [SerializeField] private float boxCost = 15f;
        [SerializeField] private int unitsPerBox = 20;
        [SerializeField] private bool cookingEnabled = true;
        [SerializeField] private int cookedStock;
        [SerializeField] private TextMesh label;

        private int _rawStock;

        public FoodDefinition Food => food;
        public int CookedStock => cookedStock;
        public int RawStock => _rawStock;

        private void OnEnable()
        {
            GameEvents.CookedFoodStored += OnCookedFoodStored;
        }

        private void OnDisable()
        {
            GameEvents.CookedFoodStored -= OnCookedFoodStored;
        }

        public void Configure(FoodDefinition boxFood, float cost, int units, bool cooking = false, int startingCooked = 0, TextMesh statusLabel = null)
        {
            food = boxFood;
            boxCost = cost;
            unitsPerBox = units;
            cookingEnabled = cooking;
            cookedStock = cooking ? Mathf.Max(0, startingCooked) : 0;
            label = statusLabel;
            RefreshLabel();
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (food == null) return string.Empty;
            string foodName = food.DisplayName;

            if (inventory.HandsFree)
            {
                if (!cookingEnabled) return $"[E] Buy a box of {foodName} (${boxCost:0.00} for {unitsPerBox} units)";
                if (cookedStock > 0) return $"[E] Take {Mathf.Min(cookedStock, inventory.FoodCapacity)} cooked {foodName} ({cookedStock} ready in the cooler)";
                if (_rawStock > 0) return $"[E] Take a raw box of {foodName} ({_rawStock} units put back) - cook it in the {food.Cooker.DisplayName()}";
                return $"[E] Buy a raw box of {foodName} (${boxCost:0.00} for {unitsPerBox} units) - cook it in the {food.Cooker.DisplayName()}";
            }

            if (inventory.IsHoldingFood && inventory.HeldFood == food)
            {
                return inventory.HeldFoodRaw
                    ? $"[E] Put the raw {foodName} box back in the cooler"
                    : $"[E] Put your {inventory.HeldFoodUnits} cooked {foodName} back in the cooler";
            }
            if (inventory.IsHoldingFood) return $"{foodName} cooler - you're holding {inventory.HeldFood.DisplayName}";
            return $"{foodName} cooler - hands full";
        }

        public string GetSecondaryPrompt(PlayerInventory inventory)
        {
            if (food == null || !cookingEnabled || !inventory.HandsFree || cookedStock <= 0) return string.Empty;
            return _rawStock > 0
                ? $"[Q] Take a raw box instead ({_rawStock} units put back)"
                : $"[Q] Buy a raw box instead (${boxCost:0.00} for {unitsPerBox} units)";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (food == null) return;

            if (inventory.HandsFree)
            {
                if (!cookingEnabled) BuyBox(inventory, false);
                else if (cookedStock > 0) TakeCooked(inventory);
                else TakeOrBuyRaw(inventory);
                return;
            }

            if (inventory.IsHoldingFood && inventory.HeldFood == food)
            {
                bool raw = inventory.HeldFoodRaw;
                int stored = inventory.RemoveFoodUnits(inventory.HeldFoodUnits);
                if (raw) _rawStock += stored;
                else cookedStock += stored;
                GameEvents.RaiseNotice($"Stored {stored} {(raw ? "raw" : "cooked")} {food.DisplayName} back in the cooler.");
                RefreshLabel();
            }
        }

        public void SecondaryInteract(PlayerInventory inventory)
        {
            if (food == null || !cookingEnabled || !inventory.HandsFree || cookedStock <= 0) return;
            TakeOrBuyRaw(inventory);
        }

        private void TakeCooked(PlayerInventory inventory)
        {
            int carried = Mathf.Min(cookedStock, inventory.FoodCapacity);
            if (carried <= 0 || !inventory.TryTakeFoodTray(food, carried)) return;
            cookedStock -= carried;
            GameEvents.RaiseNotice($"Took {carried} cooked {food.DisplayName} from the cooler ({cookedStock} left).");
            RefreshLabel();
        }

        private void TakeOrBuyRaw(PlayerInventory inventory)
        {
            if (_rawStock > 0)
            {
                int carried = Mathf.Min(_rawStock, inventory.FoodCapacity);
                if (carried <= 0 || !inventory.TryTakeFoodTray(food, carried, true)) return;
                _rawStock -= carried;
                GameEvents.RaiseNotice($"Took a raw box of {food.DisplayName} ({carried} units) from the cooler.");
                RefreshLabel();
                return;
            }
            BuyBox(inventory, true);
        }

        private void BuyBox(PlayerInventory inventory, bool raw)
        {
            var request = new PurchaseRequest(boxCost, $"Box of {food.DisplayName}", transform.position);
            GameEvents.RaisePurchaseRequested(request);
            if (!request.Approved) return;

            // A fortune can shrink what your hands manage; the rest of the box stays in the cooler.
            int carried = Mathf.Min(unitsPerBox, inventory.FoodCapacity);
            int leftover = unitsPerBox - carried;
            inventory.TryTakeFoodTray(food, carried, raw);
            if (raw) _rawStock += leftover;
            else cookedStock += leftover;
            string what = raw ? $"a raw box of {food.DisplayName}" : $"a box of {food.DisplayName}";
            GameEvents.RaiseNotice(leftover > 0
                ? $"Bought {what} for ${boxCost:0.00}, but your hands only manage {carried} of the {unitsPerBox} units; the rest stays in the cooler."
                : raw
                    ? $"Bought {what}: {unitsPerBox} frozen units for ${boxCost:0.00}. Cook them in the {food.Cooker.DisplayName()}."
                    : $"Bought {what}: {unitsPerBox} units for ${boxCost:0.00}.");
            RefreshLabel();
        }

        private void OnCookedFoodStored(FoodDefinition storedFood, int units, Vector3 at)
        {
            if (storedFood != food || units <= 0) return;
            cookedStock += units;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (label == null || food == null) return;
            label.text = cookingEnabled
                ? $"{food.DisplayName}\n{cookedStock} cooked{(_rawStock > 0 ? $" / {_rawStock} raw" : "")}"
                : food.DisplayName;
        }
    }
}
