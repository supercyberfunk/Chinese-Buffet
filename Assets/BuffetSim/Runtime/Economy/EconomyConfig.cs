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
        [Tooltip("How long the end-of-day summary stays up before the next day opens (the fountain is open for part of it).")]
        [SerializeField] private float summarySeconds = 20f;
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
        [SerializeField] private float dineAndDashHesitation = 3f;
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

        [Header("Robbery (its own counter, outside the event cap)")]
        [Tooltip("A robbery is rolled once every this many customers (a random count between min and max).")]
        [SerializeField] private int robberyEveryMinCustomers = 15;
        [SerializeField] private int robberyEveryMaxCustomers = 20;
        [Range(0f, 1f)] [SerializeField] private float robberyChance = 0.25f;
        [Tooltip("Share of the till the robber takes (a random value between min and max).")]
        [Range(0f, 1f)] [SerializeField] private float robberyMinTake = 0.02f;
        [Range(0f, 1f)] [SerializeField] private float robberyMaxTake = 0.10f;
        [Tooltip("Extra on top of the take when the player stops the robber (+20% in the notes).")]
        [SerializeField] private float robberyCatchBonus = 0.20f;
        [SerializeField] private int robberiesPerDay = 1;

        [Header("Player wallet")]
        [Tooltip("The player's own cash before the first payout, so the slot machine works on day 1.")]
        [SerializeField] private float startingPlayerCash = 20f;

        [Header("Repairs and fines")]
        [SerializeField] private float glassPaneCost = 20f;
        [SerializeField] private float ductTapeCost = 5f;
        [SerializeField] private int ductTapeStrips = 3;
        [Tooltip("The kitchen shelf gets this many lightbulbs every morning.")]
        [SerializeField] private int lightbulbsPerDay = 6;
        [Tooltip("What the landlord charges at close for a hole in the wall nobody patched.")]
        [SerializeField] private float landlordPatchFee = 40f;
        [Tooltip("What a customer's lawyer gets when an event is ignored long enough (toilet, ceiling tile).")]
        [SerializeField] private float lawsuitFine = 50f;

        [Header("Cooking")]
        [Tooltip("On: cooler boxes come out frozen and go through a cooker before they fill a tray. Off: instant trays, like the first demo.")]
        [SerializeField] private bool cookingEnabled = true;
        [Tooltip("Cooked units of each food already sitting in the cooler on day 1.")]
        [SerializeField] private int startingCookedStock = 20;
        [SerializeField] private float fryerSeconds = 20f;
        [SerializeField] private float wokSeconds = 25f;
        [SerializeField] private float steamerSeconds = 25f;
        [SerializeField] private float riceCookerSeconds = 30f;
        [Tooltip("Clean flips the wok minigame needs before the timer starts.")]
        [SerializeField] private int wokFlipsNeeded = 4;
        [Tooltip("Width of the sweet spot on the wok gauge, as a fraction of the gauge. A flip outside it throws a unit on the floor.")]
        [Range(0.1f, 0.9f)] [SerializeField] private float wokFlipWindow = 0.3f;
        [Tooltip("Seconds the fryer basket takes to lower (hold E).")]
        [SerializeField] private float fryerBasketSeconds = 1.5f;
        [Tooltip("Finished food waits this long in the cooker before it starts to burn.")]
        [SerializeField] private float burnGraceSeconds = 30f;
        [Tooltip("After the grace period, a share of the batch burns every this many seconds.")]
        [SerializeField] private float burnStepSeconds = 30f;
        [Range(0f, 1f)] [SerializeField] private float burnStepFraction = 0.25f;

        [Header("To-go phone orders")]
        [SerializeField] private bool phoneOrdersEnabled = true;
        [SerializeField] private float phoneFirstCallSeconds = 60f;
        [Tooltip("Gap between one call ending and the next ringing (a random value between min and max, shortened by satisfaction).")]
        [SerializeField] private float phoneCallGapMin = 45f;
        [SerializeField] private float phoneCallGapMax = 90f;
        [SerializeField] private int phoneCallsDayOne = 3;
        [SerializeField] private int phoneCallsAddedPerDay = 1;
        [SerializeField] private int phoneCallsMax = 8;
        [Tooltip("How long the phone rings before the caller gives up.")]
        [SerializeField] private float phoneRingSeconds = 20f;
        [Tooltip("How long the caller's words stay on screen. After that you have to remember the order.")]
        [SerializeField] private float phoneReadSeconds = 10f;
        [SerializeField] private float phoneOrderSeconds = 150f;
        [SerializeField] private int phoneOrderMinItems = 2;
        [SerializeField] private int phoneOrderMaxItems = 4;
        [SerializeField] private int phoneOrderMaxUnits = 5;
        [Tooltip("To-go is priced per unit, not per head.")]
        [SerializeField] private float toGoUnitPrice = 3f;
        [SerializeField] private int toGoBoxMaxPerItem = 3;
        [SerializeField] private int toGoBoxMaxUnits = 10;
        [SerializeField] private float phoneMissedReputation = -1f;
        [SerializeField] private float phoneExpiredReputation = -3f;

        [Header("Slot machine (chance per pull; whatever is left over pays nothing)")]
        [Tooltip("Paid from the player's own wallet, never the till.")]
        [SerializeField] private float slotPullCost = 5f;
        [SerializeField] private float slotSpinSeconds = 3f;
        [Range(0f, 1f)] [SerializeField] private float slotCookieChance = 0.30f;
        [Range(0f, 1f)] [SerializeField] private float slotEggRollChance = 0.10f;
        [SerializeField] private float slotEggRollPayout = 5f;
        [Range(0f, 1f)] [SerializeField] private float slotCatChance = 0.05f;
        [SerializeField] private float slotCatPayout = 15f;
        [Range(0f, 1f)] [SerializeField] private float slotDragonChance = 0.02f;
        [SerializeField] private float slotDragonPayout = 50f;
        [Range(0f, 1f)] [SerializeField] private float slotEightsChance = 0.005f;
        [SerializeField] private float slotEightsPayout = 200f;
        [Tooltip("Cookie chance once the wall of fortune is full.")]
        [Range(0f, 1f)] [SerializeField] private float slotCookieChanceAfterWall = 0.5f;
        [Tooltip("What was already rattling around the machine's cash box before you.")]
        [SerializeField] private float slotCashBoxStart = 412f;
        [SerializeField] private int pocketCookieLimit = 5;
        [SerializeField] private float quarterValue = 0.25f;

        [Header("Fountain")]
        [Tooltip("The night's wishing money lands between min and max, at satisfaction percent of the way.")]
        [SerializeField] private float fountainMinPayout = 2f;
        [SerializeField] private float fountainMaxPayout = 8f;
        [SerializeField] private float fountainOpenSeconds = 15f;
        [Tooltip("Coins scooped per second of holding E in the water (a random count between min and max).")]
        [SerializeField] private int fountainScoopMinCoins = 1;
        [SerializeField] private int fountainScoopMaxCoins = 3;

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

        public int RobberyEveryMinCustomers => robberyEveryMinCustomers;
        public int RobberyEveryMaxCustomers => robberyEveryMaxCustomers;
        public float RobberyChance => robberyChance;
        public float RobberyMinTake => robberyMinTake;
        public float RobberyMaxTake => robberyMaxTake;
        public float RobberyCatchBonus => robberyCatchBonus;
        public int RobberiesPerDay => robberiesPerDay;

        public float StartingPlayerCash => startingPlayerCash;

        public float GlassPaneCost => glassPaneCost;
        public float DuctTapeCost => ductTapeCost;
        public int DuctTapeStrips => ductTapeStrips;
        public int LightbulbsPerDay => lightbulbsPerDay;
        public float LandlordPatchFee => landlordPatchFee;
        public float LawsuitFine => lawsuitFine;

        public bool CookingEnabled => cookingEnabled;
        public int StartingCookedStock => startingCookedStock;
        public float FryerSeconds => fryerSeconds;
        public float WokSeconds => wokSeconds;
        public float SteamerSeconds => steamerSeconds;
        public float RiceCookerSeconds => riceCookerSeconds;
        public int WokFlipsNeeded => wokFlipsNeeded;
        public float WokFlipWindow => wokFlipWindow;
        public float FryerBasketSeconds => fryerBasketSeconds;
        public float BurnGraceSeconds => burnGraceSeconds;
        public float BurnStepSeconds => burnStepSeconds;
        public float BurnStepFraction => burnStepFraction;

        public bool PhoneOrdersEnabled => phoneOrdersEnabled;
        public float PhoneFirstCallSeconds => phoneFirstCallSeconds;
        public float PhoneCallGapMin => phoneCallGapMin;
        public float PhoneCallGapMax => phoneCallGapMax;
        public int PhoneCallsDayOne => phoneCallsDayOne;
        public int PhoneCallsAddedPerDay => phoneCallsAddedPerDay;
        public int PhoneCallsMax => phoneCallsMax;
        public float PhoneRingSeconds => phoneRingSeconds;
        public float PhoneReadSeconds => phoneReadSeconds;
        public float PhoneOrderSeconds => phoneOrderSeconds;
        public int PhoneOrderMinItems => phoneOrderMinItems;
        public int PhoneOrderMaxItems => phoneOrderMaxItems;
        public int PhoneOrderMaxUnits => phoneOrderMaxUnits;
        public float ToGoUnitPrice => toGoUnitPrice;
        public int ToGoBoxMaxPerItem => toGoBoxMaxPerItem;
        public int ToGoBoxMaxUnits => toGoBoxMaxUnits;
        public float PhoneMissedReputation => phoneMissedReputation;
        public float PhoneExpiredReputation => phoneExpiredReputation;

        public float SlotPullCost => slotPullCost;
        public float SlotSpinSeconds => slotSpinSeconds;
        public float SlotCookieChance => slotCookieChance;
        public float SlotEggRollChance => slotEggRollChance;
        public float SlotEggRollPayout => slotEggRollPayout;
        public float SlotCatChance => slotCatChance;
        public float SlotCatPayout => slotCatPayout;
        public float SlotDragonChance => slotDragonChance;
        public float SlotDragonPayout => slotDragonPayout;
        public float SlotEightsChance => slotEightsChance;
        public float SlotEightsPayout => slotEightsPayout;
        public float SlotCookieChanceAfterWall => slotCookieChanceAfterWall;
        public float SlotCashBoxStart => slotCashBoxStart;
        public int PocketCookieLimit => pocketCookieLimit;
        public float QuarterValue => quarterValue;

        public float FountainMinPayout => fountainMinPayout;
        public float FountainMaxPayout => fountainMaxPayout;
        public float FountainOpenSeconds => fountainOpenSeconds;
        public int FountainScoopMinCoins => fountainScoopMinCoins;
        public int FountainScoopMaxCoins => fountainScoopMaxCoins;

        public static EconomyConfig CreateDefault()
        {
            var config = CreateInstance<EconomyConfig>();
            config.name = "EconomyConfig (runtime default)";
            return config;
        }
    }
}
