using BuffetSim.Food;
using UnityEngine;

namespace BuffetSim.Buffet
{
    /// <summary>Somewhere a customer can take food from. Buffet trays implement this.</summary>
    public interface IFoodSource
    {
        FoodDefinition Food { get; }
        int Units { get; }

        /// <summary>Where a customer should stand to grab from this source.</summary>
        Vector3 StandPosition { get; }

        /// <summary>Removes up to <paramref name="requested"/> units and returns how many were actually taken.</summary>
        int Take(int requested);
    }
}
