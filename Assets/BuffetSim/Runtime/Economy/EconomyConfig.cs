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

        [Header("Day cycle")]
        [Tooltip("Length of one business day in real seconds (the notes aim for a 12-15 minute day).")]
        [SerializeField] private float dayLengthSeconds = 720f;
        [Tooltip("The spawner stops letting customers in this many seconds before close.")]
        [SerializeField] private float lastCallSeconds = 60f;
        [Tooltip("After close, how long the stragglers get to finish before the lights go off.")]
        [SerializeField] private float closingGraceSeconds = 45f;
        [Tooltip("How long the end-of-day summary stays up before the next day opens.")]
        [SerializeField] private float summarySeconds = 12f;
        [Tooltip("Share of the day's profit the store keeps; the rest is the player's cut (60/40 in the notes).")]
        [Range(0f, 1f)] [SerializeField] private float storeShare = 0.6f;

        [Header("Customer satisfaction (0..100)")]
        [SerializeField] private float startingReputation = 50f;
        [Tooltip("Change when a customer pays in full.")]
        [SerializeField] private float reputationPaidInFull = 2f;
        [Tooltip("Change when a customer pays but was short-changed on units.")]
        [SerializeField] private float reputationShortChanged = -1f;
        [Tooltip("Change when a customer walks out of the line.")]
        [SerializeField] private float reputationWalkout = -3f;
        [Tooltip("Change when a customer slips on a spill.")]
        [SerializeField] private float reputationSlip = -2f;
        [Tooltip("Spawn interval multiplier at 0 satisfaction (slower) and at 100 (faster).")]
        [SerializeField] private float spawnScaleAtZeroReputation = 1.5f;
        [SerializeField] private float spawnScaleAtFullReputation = 0.7f;
        [Tooltip("Bribe handed to a customer who slips on a spill so they don't call anyone.")]
        [SerializeField] private float slipBribe = 10f;

        [Header("Dine and dash")]
        [Range(0f, 1f)] [SerializeField] private float dineAndDashChance = 0.25f;
        [Tooltip("Seconds the dasher looks around nervously (the '!' and audio cue) before bolting.")]
        [SerializeField] private float dineAndDashHesitation = 2f;
        [Tooltip("Run speed of a dasher; the player walks at 4.5 and sprints at 7.5.")]
        [SerializeField] private float dineAndDashSpeed = 3.8f;
        [Tooltip("Extra on top of the bill when the player tackles a dasher (+10% in the notes).")]
        [SerializeField] private float dineAndDashBonus = 0.10f;
        [Tooltip("How long a knocked-out NPC stays on the floor before shuffling out.")]
        [SerializeField] private float knockoutSeconds = 25f;
        [Tooltip("How many coins a knocked-out dasher or thief sprays.")]
        [SerializeField] private int coinsPerBurst = 8;

        [Header("Chaos events")]
        [Tooltip("The clock checks for a new event this often (30-60 s in the notes).")]
        [SerializeField] private float eventCheckInterval = 40f;
        [Range(0f, 1f)] [SerializeField] private float eventChance = 0.25f;
        [SerializeField] private int maxEventsPerDay = 4;
        [Tooltip("Minimum seconds between two events.")]
        [SerializeField] private float eventMinGap = 45f;
        [Tooltip("No events for this long after the doors open.")]
        [SerializeField] private float eventQuietStart = 40f;

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

        public float DayLengthSeconds => dayLengthSeconds;
        public float LastCallSeconds => lastCallSeconds;
        public float ClosingGraceSeconds => closingGraceSeconds;
        public float SummarySeconds => summarySeconds;
        public float StoreShare => storeShare;

        public float StartingReputation => startingReputation;
        public float ReputationPaidInFull => reputationPaidInFull;
        public float ReputationShortChanged => reputationShortChanged;
        public float ReputationWalkout => reputationWalkout;
        public float ReputationSlip => reputationSlip;
        public float SpawnScaleAtZeroReputation => spawnScaleAtZeroReputation;
        public float SpawnScaleAtFullReputation => spawnScaleAtFullReputation;
        public float SlipBribe => slipBribe;

        public float DineAndDashChance => dineAndDashChance;
        public float DineAndDashHesitation => dineAndDashHesitation;
        public float DineAndDashSpeed => dineAndDashSpeed;
        public float DineAndDashBonus => dineAndDashBonus;
        public float KnockoutSeconds => knockoutSeconds;
        public int CoinsPerBurst => coinsPerBurst;

        public float EventCheckInterval => eventCheckInterval;
        public float EventChance => eventChance;
        public int MaxEventsPerDay => maxEventsPerDay;
        public float EventMinGap => eventMinGap;
        public float EventQuietStart => eventQuietStart;

        public static EconomyConfig CreateDefault()
        {
            var config = CreateInstance<EconomyConfig>();
            config.name = "EconomyConfig (runtime default)";
            return config;
        }
    }
}
