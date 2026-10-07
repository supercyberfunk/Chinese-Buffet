using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.ToGo
{
    /// <summary>A stack of clamshell boxes on the front counter. Take one empty-handed; put an empty one back.</summary>
    public sealed class ToGoBoxStack : MonoBehaviour, IInteractable
    {
        public string GetPrompt(PlayerInventory inventory)
        {
            if (inventory.HandsFree) return "[E] Take a to-go box";
            if (inventory.IsHoldingToGoBox)
                return inventory.ToGoUnits == 0 ? "[E] Put the empty box back" : "To-go boxes - rack or trash the box you're holding";
            return "To-go boxes - hands full";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (inventory.HandsFree)
            {
                if (inventory.TryTakeToGoBox())
                    GameEvents.RaiseNotice("Took a to-go box. Fill it from the buffet trays (E on a tray), then rack it by the door.");
                return;
            }

            if (inventory.IsHoldingToGoBox && inventory.ToGoUnits == 0)
            {
                inventory.TakeToGoBox();
                GameEvents.RaiseNotice("Put the box back on the stack.");
            }
        }
    }
}
