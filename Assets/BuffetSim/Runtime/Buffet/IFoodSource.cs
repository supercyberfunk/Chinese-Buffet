using BuffetSim.Food;
using UnityEngine;

namespace BuffetSim.Buffet
{
    /// <summary>Somewhere a customer can take food from. Buffet trays implement this.</summary>
    public interface IFoodSource
    {
        FoodDefinition Food { get; }
        int Units { get; }
        int Capacity { get; }

        /// <summary>Where a customer should stand to grab from this source.</summary>
        Vector3 StandPosition { get; }

        /// <summary>Where the food itself is (the pan on the counter): flames and smoke go here, not where the customer stands.</summary>
        Vector3 Position { get; }

        /// <summary>Removes up to <paramref name="requested"/> units and returns how many were actually taken.</summary>
        int Take(int requested);

        /// <summary>Adds up to <paramref name="offered"/> units (never past capacity) and returns how many went in.</summary>
        int Refill(int offered);
    }
}
