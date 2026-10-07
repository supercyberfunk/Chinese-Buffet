using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    /// <summary>
    /// A tupperware box of one food in the storage cooler. Empty-handed: buy a wholesale box
    /// ($15 for 20 units) and carry it as a tray. Holding the same food: put it back.
    /// </summary>
    public sealed class StorageBox : MonoBehaviour, IInteractable
    {
        [SerializeField] private FoodDefinition food;
        [SerializeField] private float boxCost = 15f;
        [SerializeField] private int unitsPerBox = 20;

        public FoodDefinition Food => food;

        public void Configure(FoodDefinition boxFood, float cost, int units)
        {
            food = boxFood;
            boxCost = cost;
            unitsPerBox = units;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (food == null) return string.Empty;
            if (inventory.HandsFree) return $"[E] Buy a box of {food.DisplayName} (${boxCost:0.00} for {unitsPerBox} units)";
            if (inventory.IsHoldingFood && inventory.HeldFood == food) return $"[E] Put your {inventory.HeldFoodUnits} {food.DisplayName} back in the cooler";
            if (inventory.IsHoldingFood) return $"{food.DisplayName} cooler - you're holding {inventory.HeldFood.DisplayName}";
            return $"{food.DisplayName} cooler - hands full";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (food == null) return;

            if (inventory.HandsFree)
            {
                var request = new PurchaseRequest(boxCost, $"Box of {food.DisplayName}", transform.position);
                GameEvents.RaisePurchaseRequested(request);
                if (!request.Approved) return;
                // A fortune can shrink what your tray holds; the rest of the box is lost to the cooler gods.
                int carried = Mathf.Min(unitsPerBox, inventory.FoodCapacity);
                inventory.TryTakeFoodTray(food, carried);
                GameEvents.RaiseNotice(carried < unitsPerBox
                    ? $"Bought a box of {food.DisplayName} for ${boxCost:0.00}, but your hands only manage {carried} of the {unitsPerBox} units."
                    : $"Bought a box of {food.DisplayName}: {unitsPerBox} units for ${boxCost:0.00}.");
                return;
            }

            if (inventory.IsHoldingFood && inventory.HeldFood == food)
            {
                int stored = inventory.RemoveFoodUnits(inventory.HeldFoodUnits);
                GameEvents.RaiseNotice($"Stored {stored} {food.DisplayName} back in the cooler.");
            }
        }
    }
}
