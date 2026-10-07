using BuffetSim.Buffet;
using BuffetSim.Economy;
using BuffetSim.Food;
using BuffetSim.Tables;
using UnityEngine;

namespace BuffetSim.Customers
{
    /// <summary>Where customers look for food. Implemented by the buffet floor registry.</summary>
    public interface IBuffetDirectory
    {
        IFoodSource FindSourceFor(FoodDefinition food);
    }

    /// <summary>Where customers ask for a table. Implemented by the buffet floor registry.</summary>
    public interface ITableDirectory
    {
        bool TryReserveTable(object customer, out DiningTable table);
    }

    /// <summary>Everything a customer needs to run its visit, handed over at spawn time.</summary>
    public sealed class CustomerContext
    {
        public EconomyConfig Config;
        public IBuffetDirectory Buffet;
        public ITableDirectory Tables;
        public CustomerQueue Queue;
        public Vector3 RegisterPoint;
        public Vector3 ExitPoint;
        public System.Random Rng = new System.Random();
        /// <summary>Until this time, dine-and-dashers trip at the door ("What runs away comes back"). Set by the spawner.</summary>
        public float DashersTripUntil = float.NegativeInfinity;
    }
}
