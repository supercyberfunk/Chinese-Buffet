using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Economy
{
    /// <summary>
    /// Owns the store's money. Nothing else mutates the balance: customers publish receipts,
    /// stations publish purchase requests, and this component answers through the event bus.
    /// </summary>
    public sealed class EconomyLedger : MonoBehaviour
    {
        [SerializeField] private EconomyConfig config;

        private float _balance;
        private float _revenueToday;
        private float _expensesToday;
        private float _deductionsToday;
        private int _customersServed;
        private int _customersLost;
        private bool _initialized;

        public float Balance => _balance;

        public void Initialize(EconomyConfig economyConfig)
        {
            config = economyConfig;
            _balance = config.StartingMoney;
            _initialized = true;
            Publish(new MoneyChange { Delta = 0f, Balance = _balance, Reason = "Opening float" });
        }

        private void OnEnable()
        {
            GameEvents.CustomerPaid += OnCustomerPaid;
            GameEvents.CustomerLost += OnCustomerLost;
            GameEvents.PurchaseRequested += OnPurchaseRequested;
            GameEvents.DishesBroken += OnDishesBroken;
        }

        private void OnDisable()
        {
            GameEvents.CustomerPaid -= OnCustomerPaid;
            GameEvents.CustomerLost -= OnCustomerLost;
            GameEvents.PurchaseRequested -= OnPurchaseRequested;
            GameEvents.DishesBroken -= OnDishesBroken;
        }

        private void OnCustomerPaid(CustomerReceipt receipt)
        {
            if (!_initialized) return;
            _customersServed++;
            _deductionsToday += receipt.Deductions;

            if (receipt.Total <= 0f)
            {
                GameEvents.RaiseNotice($"{receipt.CustomerName} left without paying: none of their {receipt.UnitsWanted} units could be served.");
                Publish(new MoneyChange { Delta = 0f, Balance = _balance, Reason = $"{receipt.CustomerName}: $0", WorldPosition = receipt.WorldPosition, HasWorldPosition = true });
                return;
            }

            _balance += receipt.Total;
            _revenueToday += receipt.Total;
            string detail = receipt.Deductions > 0f
                ? $"{receipt.CustomerName} paid ${receipt.Total:0.00} (${receipt.BaseAmount:0.00} minus ${receipt.Deductions:0.00} for {receipt.UnitsWanted - receipt.UnitsTaken} missing units)"
                : $"{receipt.CustomerName} paid ${receipt.Total:0.00} in full";
            GameEvents.RaiseNotice(detail);
            Publish(new MoneyChange { Delta = receipt.Total, Balance = _balance, Reason = receipt.CustomerName, WorldPosition = receipt.WorldPosition, HasWorldPosition = true });
        }

        private void OnCustomerLost(string customerName, Vector3 at)
        {
            if (!_initialized) return;
            _customersLost++;
            PublishSnapshot();
        }

        private void OnPurchaseRequested(PurchaseRequest request)
        {
            if (!_initialized || request == null || request.Approved) return;
            if (request.Amount > _balance)
            {
                GameEvents.RaiseNotice($"Not enough money for {request.Reason} (${request.Amount:0.00}, store has ${_balance:0.00}).");
                return;
            }

            _balance -= request.Amount;
            _expensesToday += request.Amount;
            request.Approved = true;
            Publish(new MoneyChange { Delta = -request.Amount, Balance = _balance, Reason = request.Reason, WorldPosition = request.WorldPosition, HasWorldPosition = true });
        }

        private void OnDishesBroken(int count, Vector3 at)
        {
            if (!_initialized || count <= 0) return;
            float penalty = count * config.BrokenDishPenalty;
            _balance = Mathf.Max(0f, _balance - penalty);
            _expensesToday += penalty;
            GameEvents.RaiseNotice($"{count} dish{(count == 1 ? "" : "es")} broken: -${penalty:0.00}");
            Publish(new MoneyChange { Delta = -penalty, Balance = _balance, Reason = "Broken dishes", WorldPosition = at, HasWorldPosition = true });
        }

        private void Publish(MoneyChange change)
        {
            GameEvents.RaiseMoneyChanged(change);
            PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            GameEvents.RaiseLedgerUpdated(new LedgerSnapshot
            {
                Balance = _balance,
                RevenueToday = _revenueToday,
                ExpensesToday = _expensesToday,
                DeductionsToday = _deductionsToday,
                CustomersServed = _customersServed,
                CustomersLost = _customersLost,
            });
        }
    }
}
