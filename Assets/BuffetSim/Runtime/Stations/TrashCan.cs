using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    /// <summary>Dump whatever you're holding. Plates thrown in the trash count as broken.</summary>
    public sealed class TrashCan : MonoBehaviour, IInteractable
    {
        public string GetPrompt(PlayerInventory inventory)
        {
            if (inventory.IsHoldingFood) return $"[E] Trash your {inventory.HeldFoodUnits} {(inventory.HeldFoodRaw ? "raw " : "")}{inventory.HeldFood.DisplayName}";
            if (inventory.IsHoldingPlates) return $"[E] Trash {inventory.HeldPlates} plates (they'll break)";
            if (inventory.IsHoldingToGoBox) return "[E] Trash the to-go box";
            if (inventory.IsHoldingItem) return $"[E] Throw away the {inventory.HeldItemName}";
            return "Trash can";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (inventory.IsHoldingFood)
            {
                GameEvents.RaiseNotice($"Trashed {inventory.HeldFoodUnits} {(inventory.HeldFoodRaw ? "raw " : "")}{inventory.HeldFood.DisplayName}.");
                inventory.DropEverything();
                return;
            }

            if (inventory.IsHoldingPlates)
            {
                int plates = inventory.HeldPlates;
                inventory.DropEverything();
                GameEvents.RaiseDishesBroken(plates, transform.position);
                return;
            }

            if (inventory.IsHoldingToGoBox)
            {
                GameEvents.RaiseNotice(inventory.ToGoUnits > 0 ? $"Trashed a to-go box with {inventory.ToGoUnits} units in it." : "Trashed an empty to-go box.");
                inventory.DropEverything();
                return;
            }

            if (inventory.IsHoldingItem)
            {
                GameEvents.RaiseNotice($"Threw the {inventory.HeldItemName} away.");
                inventory.DropEverything();
            }
        }
    }
}
