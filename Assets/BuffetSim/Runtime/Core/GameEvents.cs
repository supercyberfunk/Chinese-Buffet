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
        public float RevenueToday;
        public float ExpensesToday;
        public float DeductionsToday;
        public int CustomersServed;
        public int CustomersLost;
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

    /// <summary>
    /// Lightweight static event bus. Systems publish here and subscribe here; none of them
    /// hold references to each other (see CLAUDE.md architecture rules).
    /// </summary>
    public static class GameEvents
    {
        public static event Action<MoneyChange> MoneyChanged;
        public static event Action<LedgerSnapshot> LedgerUpdated;
        public static event Action<CustomerReceipt> CustomerPaid;
        public static event Action<string, Vector3> CustomerLost;
        public static event Action<PurchaseRequest> PurchaseRequested;
        public static event Action<int, Vector3> DishesBroken;
        public static event Action<string> Notice;
        public static event Action<string> PromptChanged;

        public static void RaiseMoneyChanged(MoneyChange change) => MoneyChanged?.Invoke(change);
        public static void RaiseLedgerUpdated(LedgerSnapshot snapshot) => LedgerUpdated?.Invoke(snapshot);
        public static void RaiseCustomerPaid(CustomerReceipt receipt) => CustomerPaid?.Invoke(receipt);
        public static void RaiseCustomerLost(string customerName, Vector3 at) => CustomerLost?.Invoke(customerName, at);
        public static void RaisePurchaseRequested(PurchaseRequest request) => PurchaseRequested?.Invoke(request);
        public static void RaiseDishesBroken(int count, Vector3 at) => DishesBroken?.Invoke(count, at);
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
            DishesBroken = null;
            Notice = null;
            PromptChanged = null;
        }
    }
}
