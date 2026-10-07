using BuffetSim.Player;

namespace BuffetSim.Interaction
{
    /// <summary>
    /// A second action on the Q key for things that need one (cancel a cook, dump a burnt batch).
    /// When the prompt is empty Q falls back to dropping whatever the player holds.
    /// </summary>
    public interface ISecondaryInteractable
    {
        string GetSecondaryPrompt(PlayerInventory inventory);

        void SecondaryInteract(PlayerInventory inventory);
    }
}
