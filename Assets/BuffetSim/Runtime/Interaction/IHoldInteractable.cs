using BuffetSim.Player;

namespace BuffetSim.Interaction
{
    /// <summary>
    /// An interactable that can ask for E to be held instead of pressed (repairs, pinning a slip,
    /// calming a customer down). While <see cref="GetHoldSeconds"/> is above zero the interactor
    /// fills a progress bar, keeps the player in place and calls <see cref="CompleteHold"/> once
    /// the bar is full; at zero or below a plain press calls <see cref="IInteractable.Interact"/>.
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        float GetHoldSeconds(PlayerInventory inventory);

        void CompleteHold(PlayerInventory inventory);
    }
}
