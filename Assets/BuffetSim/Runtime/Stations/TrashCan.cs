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
            if (inventory.IsHoldingFood) return $"[E] Trash your {inventory.HeldFoodUnits} {inventory.HeldFood.DisplayName}";
            if (inventory.IsHoldingPlates) return $"[E] Trash {inventory.HeldPlates} plates (they'll break)";
            return "Trash can";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (inventory.IsHoldingFood)
            {
                GameEvents.RaiseNotice($"Trashed {inventory.HeldFoodUnits} {inventory.HeldFood.DisplayName}.");
                inventory.DropEverything();
                return;
            }

            if (inventory.IsHoldingPlates)
            {
                int plates = inventory.HeldPlates;
                inventory.DropEverything();
                GameEvents.RaiseDishesBroken(plates, transform.position);
            }
        }
    }
}
