using System;
using BuffetSim.Food;
using UnityEngine;

namespace BuffetSim.Core
{
    /// <summary>A change to the store's balance, published by the economy so UI/audio can react.</summary>
    public struct MoneyChange
    {
        public float Delta;
        public float Balance;
        public string Reason;
        public Vector3 WorldPosition;
        public bool HasWorldPosition;
    }

    /// <summary>Read-only picture of the day's numbers for the HUD.</summary>
    public struct LedgerSnapshot
    {
        public float Balance;
        /// <summary>The player's own cut, paid out at the end of each day (the 40% from the notes).</summary>
        public float PlayerCash;
        public float RevenueToday;
        public float ExpensesToday;
        public float DeductionsToday;
        public int CustomersServed;
        public int CustomersLost;
        public int DineAndDashesToday;
        public int DashersCaughtToday;
    }

    /// <summary>What a customer paid (or didn't) on the way out.</summary>
    public struct CustomerReceipt
    {
        public string CustomerName;
        public float BaseAmount;
        public float Deductions;
        public int UnitsWanted;
        public int UnitsTaken;
        public float Total;
        public Vector3 WorldPosition;
    }

    /// <summary>
    /// A request to spend store money. The economy handles it synchronously and sets
    /// <see cref="Approved"/>; the requester never touches the balance directly.
    /// </summary>
    public sealed class PurchaseRequest
    {
        public float Amount { get; }
        public string Reason { get; }
        public Vector3 WorldPosition { get; }
        public bool Approved { get; set; }

        public PurchaseRequest(float amount, string reason, Vector3 worldPosition)
        {
            Amount = amount;
            Reason = reason;
            WorldPosition = worldPosition;
        }
    }

    /// <summary>Where the business day is: open, last call, closing up, or closed with the summary up.</summary>
    public enum DayPhase
    {
        Open,
        LastCall,
        Closing,
        Closed,
    }

    /// <summary>The clock's current reading, published every tick and on every phase change.</summary>
    public struct DayClockSnapshot
    {
        public int Day;
        public DayPhase Phase;
        public float SecondsRemaining;
        public float DayLengthSeconds;
    }

    /// <summary>End-of-day numbers the ledger publishes when the doors close.</summary>
    public struct DaySummary
    {
        public int Day;
        public float Revenue;
        public float Expenses;
        public float Deductions;
        public float Profit;
        public float StoreShare;
        public float PlayerShare;
        public float PlayerCashTotal;
        public int CustomersServed;
        public int CustomersLost;
        public int DineAndDashes;
        public int DashersCaught;
        public float Reputation;
    }

    /// <summary>Identifies a chaos event to presentation systems without handing them the runner.</summary>
    public struct ChaosEventInfo
    {
        public string Id;
        public string DisplayName;
        public string Description;
    }

    /// <summary>
    /// A thief asking the economy for store money. The ledger fills <see cref="Taken"/> synchronously
    /// (never more than the balance) and the thief carries exactly that amount away.
    /// </summary>
    public sealed class TheftRequest
    {
        public float RequestedAmount { get; }
        /// <summary>When above zero the thief asks for this share of whatever is in the till instead of a fixed amount.</summary>
        public float RequestedFraction { get; }
        public string Thief { get; }
        public Vector3 WorldPosition { get; }
        public float Taken { get; set; }

        public TheftRequest(float requestedAmount, string thief, Vector3 worldPosition)
        {
            RequestedAmount = requestedAmount;
            Thief = thief;
            WorldPosition = worldPosition;
        }

        public TheftRequest(string thief, float fractionOfTill, Vector3 worldPosition)
        {
            RequestedFraction = Mathf.Clamp01(fractionOfTill);
            Thief = thief;
            WorldPosition = worldPosition;
        }
    }

    /// <summary>
    /// A request to spend the player's own cash (the 40% cut, never the till). The ledger answers
    /// synchronously and sets <see cref="Approved"/>; the slot machine is the main customer.
    /// </summary>
    public sealed class WalletSpendRequest
    {
        public float Amount { get; }
        public string Reason { get; }
        public Vector3 WorldPosition { get; }
        public bool Approved { get; set; }

        public WalletSpendRequest(float amount, string reason, Vector3 worldPosition)
        {
            Amount = amount;
            Reason = reason;
            WorldPosition = worldPosition;
        }
    }

    /// <summary>Things fortunes and events can do to the player; <see cref="PlayerEffect"/> carries the numbers.</summary>
    public enum PlayerEffectKind
    {
        /// <summary>No sprinting for Seconds.</summary>
        SprintDisabled,
        /// <summary>Walk and sprint speed times Value for Seconds.</summary>
        SpeedMultiplier,
        /// <summary>Sprint speed only times Value for Seconds.</summary>
        SprintMultiplier,
        /// <summary>Spills can't take you down for Seconds.</summary>
        NoSlip,
        /// <summary>You slip on dry floor every Value seconds for Seconds.</summary>
        SlipEvery,
        /// <summary>Plate cap becomes Value for Seconds.</summary>
        PlateCapacity,
        /// <summary>Unit cap on a carried tray becomes Value for Seconds.</summary>
        FoodCapacity,
        /// <summary>Everything in your hands hits the floor (plates break).</summary>
        DropEverything,
        /// <summary>Lifted into the sky for Seconds, then dropped at Position.</summary>
        Abduct,
        /// <summary>Flat on the floor for Seconds.</summary>
        KnockDown,
        /// <summary>Moved to Position.</summary>
        Teleport,
    }

    public struct PlayerEffect
    {
        public PlayerEffectKind Kind;
        public float Seconds;
        public float Value;
        public Vector3 Position;
        /// <summary>Who asked, for the log ("a fortune", "aliens").</summary>
        public string Source;
    }

    /// <summary>Rules the ledger applies to upcoming bills.</summary>
    public enum BillModifierKind
    {
        /// <summary>The next Count bills are comped to $0.</summary>
        CompNext,
        /// <summary>The next Count bills are multiplied by Multiplier.</summary>
        BoostNext,
        /// <summary>For Seconds, missing units deduct nothing and broken dishes cost nothing.</summary>
        ForgiveMistakes,
    }

    public struct BillModifier
    {
        public BillModifierKind Kind;
        public int Count;
        public float Multiplier;
        public float Seconds;
        public string Source;
    }

    /// <summary>A phone order as the HUD sees it. The caller's words are only shown while the call lasts.</summary>
    public struct ToGoOrderInfo
    {
        public int Id;
        public string CallerName;
        /// <summary>What the caller said, word for word. The memory game is remembering it.</summary>
        public string Script;
        public int TotalUnits;
        public int ItemCount;
        public float SecondsAllowed;
        public float SecondsLeft;
        /// <summary>True while the caller is still talking (the script is on screen).</summary>
        public bool OnTheLine;
    }

    /// <summary>
    /// A packed to-go box offered at the pickup shelf. The phone service answers synchronously:
    /// <see cref="Accepted"/> means the box was taken and paid for.
    /// </summary>
    public sealed class ToGoDeliveryRequest
    {
        public FoodDefinition[] Foods;
        public int[] Units;
        public Vector3 WorldPosition;
        public bool Accepted;
        public string Outcome = string.Empty;

        public int TotalUnits
        {
            get
            {
                int total = 0;
                if (Units != null) for (int i = 0; i < Units.Length; i++) total += Units[i];
                return total;
            }
        }

        public int CountOf(FoodDefinition food)
        {
            if (Foods == null || Units == null) return 0;
            for (int i = 0; i < Foods.Length && i < Units.Length; i++)
                if (Foods[i] == food) return Units[i];
            return 0;
        }
    }

    public enum FortuneKind
    {
        General,
        Detrimental,
        Positive,
        Event,
    }

    /// <summary>A cracked fortune cookie: the slip and what it did.</summary>
    public struct FortuneReveal
    {
        public int Id;
        public string Text;
        public FortuneKind Kind;
        public string EffectSummary;
    }

    /// <summary>
    /// Lightweight static event bus. Systems publish here and subscribe here; none of them
    /// hold references to each other (see CLAUDE.md architecture rules).
    /// </summary>
    public static class GameEvents
    {
        // Money
        public static event Action<MoneyChange> MoneyChanged;
        public static event Action<LedgerSnapshot> LedgerUpdated;
        public static event Action<CustomerReceipt> CustomerPaid;
        public static event Action<string, Vector3> CustomerLost;
        public static event Action<PurchaseRequest> PurchaseRequested;
        public static event Action<TheftRequest> TheftRequested;
        /// <summary>Cash picked up off the floor (knocked-out dashers, thieves): amount, reason, where.</summary>
        public static event Action<float, string, Vector3> MoneyRecovered;
        public static event Action<int, Vector3> DishesBroken;
        public static event Action<WalletSpendRequest> WalletSpendRequested;
        /// <summary>Cash that goes straight into the player's own pocket, outside the split (slot payouts, the fountain, quarters off the floor).</summary>
        public static event Action<float, string, Vector3> WalletCredited;
        /// <summary>The player's own cash changed: new total, delta, reason.</summary>
        public static event Action<float, float, string> WalletChanged;
        /// <summary>A bill the store has to eat (fines, the landlord, the plumber). Never takes the till below zero.</summary>
        public static event Action<float, string, Vector3> ExpenseCharged;
        /// <summary>A rule for upcoming bills (comped, boosted, forgiven); the ledger keeps track.</summary>
        public static event Action<BillModifier> BillModifierRequested;

        // Day cycle
        public static event Action<int> DayStarted;
        public static event Action<DayClockSnapshot> DayClockTicked;
        public static event Action<DayClockSnapshot> DayPhaseChanged;
        /// <summary>The doors have closed for the day; the ledger answers with <see cref="DaySummaryReady"/>.</summary>
        public static event Action<int> DayEnded;
        public static event Action<DaySummary> DaySummaryReady;

        // Customers
        /// <summary>How many customers are currently in the building (line, buffet, tables, leaving).</summary>
        public static event Action<int> CustomerCountChanged;
        public static event Action<string, Vector3> DineAndDashStarted;
        /// <summary>Customer name, whether the player caught them, the money involved.</summary>
        public static event Action<string, bool, float> DineAndDashResolved;
        public static event Action<string, Vector3> CustomerSlipped;
        /// <summary>Let this many customers in right now, line or no line.</summary>
        public static event Action<int> CustomerSpawnRequested;
        /// <summary>The next N customers roll the biggest order there is.</summary>
        public static event Action<int> OrderBoostRequested;
        /// <summary>For this many seconds, dine-and-dashers trip at the door.</summary>
        public static event Action<float> DashersTripRequested;
        /// <summary>The seated customer nearest the target gets up and follows it for the given seconds.</summary>
        public static event Action<Transform, float> CustomerFollowRequested;

        // Player
        public static event Action<PlayerEffect> PlayerEffectRequested;

        // Kitchen and floor
        /// <summary>Cooked food went into the cooler: which food, how many units, where it was cooked.</summary>
        public static event Action<FoodDefinition, int, Vector3> CookedFoodStored;
        /// <summary>Every cooker timer runs at this multiplier for the given seconds.</summary>
        public static event Action<float, float> CookingSpeedRequested;
        public static event Action DishwasherRunRequested;
        /// <summary>Someone other than the player loaded dirty plates into the dishwasher.</summary>
        public static event Action<int, Vector3> PlatesDelivered;
        /// <summary>The buffet tray nearest this point refills to the top.</summary>
        public static event Action<Vector3> TrayRefillRequested;

        // To-go orders
        public static event Action<bool> PhoneRingingChanged;
        public static event Action PhoneRingRequested;
        public static event Action<ToGoOrderInfo> ToGoOrderPlaced;
        public static event Action<ToGoOrderInfo> ToGoOrderTicked;
        /// <summary>The order, whether it was delivered, a one-line outcome.</summary>
        public static event Action<ToGoOrderInfo, bool, string> ToGoOrderEnded;
        /// <summary>A box put on the pickup shelf; whoever holds the open order answers in place.</summary>
        public static event Action<ToGoDeliveryRequest> ToGoDeliveryRequested;

        // Fortunes and gambling
        public static event Action<Vector3> FortuneCookieCracked;
        public static event Action<FortuneReveal> FortuneRevealed;
        /// <summary>Pinned fortunes, fortunes in the catalog, slips in your pocket waiting to be pinned.</summary>
        public static event Action<int, int, int> FortuneWallChanged;
        /// <summary>The wall of fortune is full: the slot machine's lock pops.</summary>
        public static event Action SlotJackpotUnlocked;

        // Satisfaction (0..100 store-wide value)
        /// <summary>Ask the reputation system to move the value: delta and the reason shown to the player.</summary>
        public static event Action<float, string> ReputationNudged;
        /// <summary>New value, the delta applied, the reason.</summary>
        public static event Action<float, float, string> ReputationChanged;

        // Chaos events
        public static event Action<ChaosEventInfo> ChaosEventStarted;
        /// <summary>The event, whether the player resolved it, a one-line outcome.</summary>
        public static event Action<ChaosEventInfo, bool, string> ChaosEventEnded;
        /// <summary>Start the event with this id now, on top of whatever is running (fortunes, robberies, debug keys).</summary>
        public static event Action<string> ChaosEventRequested;

        // Presentation
        public static event Action<string> Notice;
        public static event Action<string> PromptChanged;
        /// <summary>Tint the room's light this colour for the given seconds (the slot machine's 8-8-8).</summary>
        public static event Action<Color, float> LightingCueRequested;

        // Debug
        public static event Action<float> DayFastForwardRequested;

        public static void RaiseMoneyChanged(MoneyChange change) => MoneyChanged?.Invoke(change);
        public static void RaiseLedgerUpdated(LedgerSnapshot snapshot) => LedgerUpdated?.Invoke(snapshot);
        public static void RaiseCustomerPaid(CustomerReceipt receipt) => CustomerPaid?.Invoke(receipt);
        public static void RaiseCustomerLost(string customerName, Vector3 at) => CustomerLost?.Invoke(customerName, at);
        public static void RaisePurchaseRequested(PurchaseRequest request) => PurchaseRequested?.Invoke(request);
        public static void RaiseTheftRequested(TheftRequest request) => TheftRequested?.Invoke(request);
        public static void RaiseMoneyRecovered(float amount, string reason, Vector3 at) => MoneyRecovered?.Invoke(amount, reason, at);
        public static void RaiseDishesBroken(int count, Vector3 at) => DishesBroken?.Invoke(count, at);
        public static void RaiseWalletSpendRequested(WalletSpendRequest request) => WalletSpendRequested?.Invoke(request);
        public static void RaiseWalletCredited(float amount, string reason, Vector3 at) => WalletCredited?.Invoke(amount, reason, at);
        public static void RaiseWalletChanged(float total, float delta, string reason) => WalletChanged?.Invoke(total, delta, reason ?? string.Empty);
        public static void RaiseExpenseCharged(float amount, string reason, Vector3 at) => ExpenseCharged?.Invoke(amount, reason, at);
        public static void RaiseBillModifierRequested(BillModifier modifier) => BillModifierRequested?.Invoke(modifier);

        public static void RaiseDayStarted(int day) => DayStarted?.Invoke(day);
        public static void RaiseDayClockTicked(DayClockSnapshot snapshot) => DayClockTicked?.Invoke(snapshot);
        public static void RaiseDayPhaseChanged(DayClockSnapshot snapshot) => DayPhaseChanged?.Invoke(snapshot);
        public static void RaiseDayEnded(int day) => DayEnded?.Invoke(day);
        public static void RaiseDaySummaryReady(DaySummary summary) => DaySummaryReady?.Invoke(summary);

        public static void RaiseCustomerCountChanged(int inStore) => CustomerCountChanged?.Invoke(inStore);
        public static void RaiseDineAndDashStarted(string customerName, Vector3 at) => DineAndDashStarted?.Invoke(customerName, at);
        public static void RaiseDineAndDashResolved(string customerName, bool caught, float amount) => DineAndDashResolved?.Invoke(customerName, caught, amount);
        public static void RaiseCustomerSlipped(string customerName, Vector3 at) => CustomerSlipped?.Invoke(customerName, at);
        public static void RaiseCustomerSpawnRequested(int count) => CustomerSpawnRequested?.Invoke(count);
        public static void RaiseOrderBoostRequested(int customers) => OrderBoostRequested?.Invoke(customers);
        public static void RaiseDashersTripRequested(float seconds) => DashersTripRequested?.Invoke(seconds);
        public static void RaiseCustomerFollowRequested(Transform target, float seconds) => CustomerFollowRequested?.Invoke(target, seconds);

        public static void RaisePlayerEffectRequested(PlayerEffect effect) => PlayerEffectRequested?.Invoke(effect);

        public static void RaiseCookedFoodStored(FoodDefinition food, int units, Vector3 at) => CookedFoodStored?.Invoke(food, units, at);
        public static void RaiseCookingSpeedRequested(float multiplier, float seconds) => CookingSpeedRequested?.Invoke(multiplier, seconds);
        public static void RaiseDishwasherRunRequested() => DishwasherRunRequested?.Invoke();
        public static void RaisePlatesDelivered(int count, Vector3 at) => PlatesDelivered?.Invoke(count, at);
        public static void RaiseTrayRefillRequested(Vector3 near) => TrayRefillRequested?.Invoke(near);

        public static void RaisePhoneRingingChanged(bool ringing) => PhoneRingingChanged?.Invoke(ringing);
        public static void RaisePhoneRingRequested() => PhoneRingRequested?.Invoke();
        public static void RaiseToGoOrderPlaced(ToGoOrderInfo order) => ToGoOrderPlaced?.Invoke(order);
        public static void RaiseToGoOrderTicked(ToGoOrderInfo order) => ToGoOrderTicked?.Invoke(order);
        public static void RaiseToGoOrderEnded(ToGoOrderInfo order, bool delivered, string outcome) => ToGoOrderEnded?.Invoke(order, delivered, outcome ?? string.Empty);
        public static void RaiseToGoDeliveryRequested(ToGoDeliveryRequest request) => ToGoDeliveryRequested?.Invoke(request);

        public static void RaiseFortuneCookieCracked(Vector3 at) => FortuneCookieCracked?.Invoke(at);
        public static void RaiseFortuneRevealed(FortuneReveal reveal) => FortuneRevealed?.Invoke(reveal);
        public static void RaiseFortuneWallChanged(int pinned, int total, int slipsCarried) => FortuneWallChanged?.Invoke(pinned, total, slipsCarried);
        public static void RaiseSlotJackpotUnlocked() => SlotJackpotUnlocked?.Invoke();

        public static void RaiseReputationNudged(float delta, string reason) => ReputationNudged?.Invoke(delta, reason);
        public static void RaiseReputationChanged(float value, float delta, string reason) => ReputationChanged?.Invoke(value, delta, reason);

        public static void RaiseChaosEventStarted(ChaosEventInfo info) => ChaosEventStarted?.Invoke(info);
        public static void RaiseChaosEventEnded(ChaosEventInfo info, bool resolved, string outcome) => ChaosEventEnded?.Invoke(info, resolved, outcome ?? string.Empty);
        public static void RaiseChaosEventRequested(string id) => ChaosEventRequested?.Invoke(id);

        public static void RaisePromptChanged(string prompt) => PromptChanged?.Invoke(prompt ?? string.Empty);
        public static void RaiseLightingCueRequested(Color color, float seconds) => LightingCueRequested?.Invoke(color, seconds);
        public static void RaiseDayFastForwardRequested(float seconds) => DayFastForwardRequested?.Invoke(seconds);

        public static void RaiseNotice(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Debug.Log($"[Buffet] {message}");
            Notice?.Invoke(message);
        }

        /// <summary>
        /// Drops every subscriber. The scene bootstrap calls this first thing so handlers from a
        /// previous play session (domain reload disabled) or a reloaded scene don't linger.
        /// </summary>
        public static void ClearAll()
        {
            MoneyChanged = null;
            LedgerUpdated = null;
            CustomerPaid = null;
            CustomerLost = null;
            PurchaseRequested = null;
            TheftRequested = null;
            MoneyRecovered = null;
            DishesBroken = null;
            WalletSpendRequested = null;
            WalletCredited = null;
            WalletChanged = null;
            ExpenseCharged = null;
            BillModifierRequested = null;

            DayStarted = null;
            DayClockTicked = null;
            DayPhaseChanged = null;
            DayEnded = null;
            DaySummaryReady = null;

            CustomerCountChanged = null;
            DineAndDashStarted = null;
            DineAndDashResolved = null;
            CustomerSlipped = null;
            CustomerSpawnRequested = null;
            OrderBoostRequested = null;
            DashersTripRequested = null;
            CustomerFollowRequested = null;

            PlayerEffectRequested = null;

            CookedFoodStored = null;
            CookingSpeedRequested = null;
            DishwasherRunRequested = null;
            PlatesDelivered = null;
            TrayRefillRequested = null;

            PhoneRingingChanged = null;
            PhoneRingRequested = null;
            ToGoOrderPlaced = null;
            ToGoOrderTicked = null;
            ToGoOrderEnded = null;
            ToGoDeliveryRequested = null;

            FortuneCookieCracked = null;
            FortuneRevealed = null;
            FortuneWallChanged = null;
            SlotJackpotUnlocked = null;

            ReputationNudged = null;
            ReputationChanged = null;

            ChaosEventStarted = null;
            ChaosEventEnded = null;
            ChaosEventRequested = null;

            Notice = null;
            PromptChanged = null;
            LightingCueRequested = null;

            DayFastForwardRequested = null;
        }
    }
}
