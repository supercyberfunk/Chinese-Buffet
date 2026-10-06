using System;
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
        public string Thief { get; }
        public Vector3 WorldPosition { get; }
        public float Taken { get; set; }

        public TheftRequest(float requestedAmount, string thief, Vector3 worldPosition)
        {
            RequestedAmount = requestedAmount;
            Thief = thief;
            WorldPosition = worldPosition;
        }
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

        // Satisfaction (0..100 store-wide value)
        /// <summary>Ask the reputation system to move the value: delta and the reason shown to the player.</summary>
        public static event Action<float, string> ReputationNudged;
        /// <summary>New value, the delta applied, the reason.</summary>
        public static event Action<float, float, string> ReputationChanged;

        // Chaos events
        public static event Action<ChaosEventInfo> ChaosEventStarted;
        /// <summary>The event, whether the player resolved it, a one-line outcome.</summary>
        public static event Action<ChaosEventInfo, bool, string> ChaosEventEnded;

        // Presentation
        public static event Action<string> Notice;
        public static event Action<string> PromptChanged;

        public static void RaiseMoneyChanged(MoneyChange change) => MoneyChanged?.Invoke(change);
        public static void RaiseLedgerUpdated(LedgerSnapshot snapshot) => LedgerUpdated?.Invoke(snapshot);
        public static void RaiseCustomerPaid(CustomerReceipt receipt) => CustomerPaid?.Invoke(receipt);
        public static void RaiseCustomerLost(string customerName, Vector3 at) => CustomerLost?.Invoke(customerName, at);
        public static void RaisePurchaseRequested(PurchaseRequest request) => PurchaseRequested?.Invoke(request);
        public static void RaiseTheftRequested(TheftRequest request) => TheftRequested?.Invoke(request);
        public static void RaiseMoneyRecovered(float amount, string reason, Vector3 at) => MoneyRecovered?.Invoke(amount, reason, at);
        public static void RaiseDishesBroken(int count, Vector3 at) => DishesBroken?.Invoke(count, at);

        public static void RaiseDayStarted(int day) => DayStarted?.Invoke(day);
        public static void RaiseDayClockTicked(DayClockSnapshot snapshot) => DayClockTicked?.Invoke(snapshot);
        public static void RaiseDayPhaseChanged(DayClockSnapshot snapshot) => DayPhaseChanged?.Invoke(snapshot);
        public static void RaiseDayEnded(int day) => DayEnded?.Invoke(day);
        public static void RaiseDaySummaryReady(DaySummary summary) => DaySummaryReady?.Invoke(summary);

        public static void RaiseCustomerCountChanged(int inStore) => CustomerCountChanged?.Invoke(inStore);
        public static void RaiseDineAndDashStarted(string customerName, Vector3 at) => DineAndDashStarted?.Invoke(customerName, at);
        public static void RaiseDineAndDashResolved(string customerName, bool caught, float amount) => DineAndDashResolved?.Invoke(customerName, caught, amount);
        public static void RaiseCustomerSlipped(string customerName, Vector3 at) => CustomerSlipped?.Invoke(customerName, at);

        public static void RaiseReputationNudged(float delta, string reason) => ReputationNudged?.Invoke(delta, reason);
        public static void RaiseReputationChanged(float value, float delta, string reason) => ReputationChanged?.Invoke(value, delta, reason);

        public static void RaiseChaosEventStarted(ChaosEventInfo info) => ChaosEventStarted?.Invoke(info);
        public static void RaiseChaosEventEnded(ChaosEventInfo info, bool resolved, string outcome) => ChaosEventEnded?.Invoke(info, resolved, outcome ?? string.Empty);

        public static void RaisePromptChanged(string prompt) => PromptChanged?.Invoke(prompt ?? string.Empty);

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

            DayStarted = null;
            DayClockTicked = null;
            DayPhaseChanged = null;
            DayEnded = null;
            DaySummaryReady = null;

            CustomerCountChanged = null;
            DineAndDashStarted = null;
            DineAndDashResolved = null;
            CustomerSlipped = null;

            ReputationNudged = null;
            ReputationChanged = null;

            ChaosEventStarted = null;
            ChaosEventEnded = null;

            Notice = null;
            PromptChanged = null;
        }
    }
}
