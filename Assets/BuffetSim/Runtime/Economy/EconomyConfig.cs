using UnityEngine;

namespace BuffetSim.Economy
{
    /// <summary>
    /// Every tunable number from the design notes lives here so designers can balance the game
    /// without touching code. The demo scene builder creates a runtime default when no asset is assigned.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Money")]
        [SerializeField] private float startingMoney = 200f;
        [Tooltip("Base value a customer pays when they finish eating.")]
        [SerializeField] private float baseCustomerBill = 35f;
        [Tooltip("Wholesale price of one box of any base food.")]
        [SerializeField] private float wholesaleBoxCost = 15f;
        [SerializeField] private int unitsPerBox = 20;
        [Tooltip("Deducted from the bill for every unit the customer wanted but could not get.")]
        [SerializeField] private float unfulfilledUnitDeduction = 1.25f;
        [SerializeField] private float brokenDishPenalty = 0.5f;

        [Header("Customer orders")]
        [SerializeField] private int minDishesPerOrder = 2;
        [SerializeField] private int maxDishesPerOrder = 6;
        [SerializeField] private int minUnitsPerVisit = 5;
        [SerializeField] private int maxUnitsPerVisit = 20;
        [SerializeField] private int maxUnitsPerItem = 10;

        [Header("Trays and plates")]
        [SerializeField] private int trayCapacity = 20;
        [Tooltip("Show the '!' warning above a tray when it drops below this many units.")]
        [SerializeField] private int lowTrayWarning = 5;
        [SerializeField] private int minPlatesPerVisit = 2;
        [SerializeField] private int maxPlatesPerVisit = 5;
        [SerializeField] private int playerPlateCapacity = 4;
        [SerializeField] private int dishwasherCapacity = 50;

        [Header("Customer flow")]
        [SerializeField] private float customerSpawnInterval = 7f;
        [SerializeField] private int maxLineLength = 6;
        [Tooltip("Roughly how long a customer tolerates standing in line before leaving.")]
        [SerializeField] private float linePatienceSeconds = 60f;
        [SerializeField] private float baseEatSeconds = 8f;
        [SerializeField] private float eatSecondsPerUnit = 0.4f;
        [SerializeField] private float grabSeconds = 1.2f;
        [SerializeField] private float tableCheckInterval = 2f;

        public float StartingMoney => startingMoney;
        public float BaseCustomerBill => baseCustomerBill;
        public float WholesaleBoxCost => wholesaleBoxCost;
        public int UnitsPerBox => unitsPerBox;
        public float UnitCost => unitsPerBox > 0 ? wholesaleBoxCost / unitsPerBox : 0f;
        public float UnfulfilledUnitDeduction => unfulfilledUnitDeduction;
        public float BrokenDishPenalty => brokenDishPenalty;

        public int MinDishesPerOrder => minDishesPerOrder;
        public int MaxDishesPerOrder => maxDishesPerOrder;
        public int MinUnitsPerVisit => minUnitsPerVisit;
        public int MaxUnitsPerVisit => maxUnitsPerVisit;
        public int MaxUnitsPerItem => maxUnitsPerItem;

        public int TrayCapacity => trayCapacity;
        public int LowTrayWarning => lowTrayWarning;
        public int MinPlatesPerVisit => minPlatesPerVisit;
        public int MaxPlatesPerVisit => maxPlatesPerVisit;
        public int PlayerPlateCapacity => playerPlateCapacity;
        public int DishwasherCapacity => dishwasherCapacity;

        public float CustomerSpawnInterval => customerSpawnInterval;
        public int MaxLineLength => maxLineLength;
        public float LinePatienceSeconds => linePatienceSeconds;
        public float BaseEatSeconds => baseEatSeconds;
        public float EatSecondsPerUnit => eatSecondsPerUnit;
        public float GrabSeconds => grabSeconds;
        public float TableCheckInterval => tableCheckInterval;

        public static EconomyConfig CreateDefault()
        {
            var config = CreateInstance<EconomyConfig>();
            config.name = "EconomyConfig (runtime default)";
            return config;
        }
    }
}
