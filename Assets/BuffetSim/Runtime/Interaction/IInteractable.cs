using BuffetSim.Player;

namespace BuffetSim.Interaction
{
    /// <summary>
    /// Anything the player can look at and press the interact key on. The player hands over its
    /// inventory so stations can inspect what is being carried without knowing about the controller.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Text shown in the HUD while the player looks at this. Empty hides the prompt.</summary>
        string GetPrompt(PlayerInventory inventory);

        void Interact(PlayerInventory inventory);
    }
}
