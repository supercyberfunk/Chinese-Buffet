using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Economy
{
    /// <summary>
    /// Owns the store's money. Nothing else mutates the balance: customers publish receipts,
    /// stations publish purchase requests, thieves publish theft requests, coins publish recoveries,
    /// and this component answers through the event bus. At the end of each day it splits the
    /// profit between the store and the player's own cash (the 60/40 from the notes).
    /// </summary>
    public sealed class EconomyLedger : MonoBehaviour
    {
        [SerializeField] private EconomyConfig config;

        private float _balance;
        private float _playerCash;
        private float _revenueToday;
        private float _expensesToday;
        private float _deductionsToday;
        private int _customersServed;
        private int _customersLost;
        private int _dineAndDashesToday;
        private int _dashersCaughtToday;
        private float _reputation;
        private bool _initialized;

        public float Balance => _balance;
        /// <summary>The player's own money, paid out of each day's profit. Never spent by the store.</summary>
        public float PlayerCash => _playerCash;

        public void Initialize(EconomyConfig economyConfig)
        {
            config = economyConfig;
            _balance = config.StartingMoney;
            _reputation = config.StartingReputation;
            _initialized = true;
            Publish(new MoneyChange { Delta = 0f, Balance = _balance, Reason = "Opening float" });
        }

        private void OnEnable()
        {
            GameEvents.CustomerPaid += OnCustomerPaid;
            GameEvents.CustomerLost += OnCustomerLost;
            GameEvents.PurchaseRequested += OnPurchaseRequested;
            GameEvents.DishesBroken += OnDishesBroken;
            GameEvents.MoneyRecovered += OnMoneyRecovered;
            GameEvents.TheftRequested += OnTheftRequested;
            GameEvents.CustomerSlipped += OnCustomerSlipped;
            GameEvents.DineAndDashResolved += OnDineAndDashResolved;
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayEnded += OnDayEnded;
            GameEvents.ReputationChanged += OnReputationChanged;
        }

        private void OnDisable()
        {
            GameEvents.CustomerPaid -= OnCustomerPaid;
            GameEvents.CustomerLost -= OnCustomerLost;
            GameEvents.PurchaseRequested -= OnPurchaseRequested;
            GameEvents.DishesBroken -= OnDishesBroken;
            GameEvents.MoneyRecovered -= OnMoneyRecovered;
            GameEvents.TheftRequested -= OnTheftRequested;
            GameEvents.CustomerSlipped -= OnCustomerSlipped;
            GameEvents.DineAndDashResolved -= OnDineAndDashResolved;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayEnded -= OnDayEnded;
            GameEvents.ReputationChanged -= OnReputationChanged;
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

        /// <summary>Coins picked up off the floor go straight back into the till and count as revenue.</summary>
        private void OnMoneyRecovered(float amount, string reason, Vector3 at)
        {
            if (!_initialized || amount <= 0f) return;
            _balance += amount;
            _revenueToday += amount;
            Publish(new MoneyChange { Delta = amount, Balance = _balance, Reason = $"Recovered: {reason}", WorldPosition = at, HasWorldPosition = true });
        }

        /// <summary>A thief asks for money; they get at most what is in the till and the request carries the answer.</summary>
        private void OnTheftRequested(TheftRequest request)
        {
            if (!_initialized || request == null || request.Taken > 0f) return;
            float taken = Mathf.Clamp(request.RequestedAmount, 0f, _balance);
            request.Taken = taken;
            if (taken <= 0f)
            {
                GameEvents.RaiseNotice($"{request.Thief} went for the till, but it's empty.");
                return;
            }

            _balance -= taken;
            _expensesToday += taken;
            GameEvents.RaiseNotice($"{request.Thief} grabbed ${taken:0.00} from the till!");
            Publish(new MoneyChange { Delta = -taken, Balance = _balance, Reason = $"Stolen by {request.Thief}", WorldPosition = request.WorldPosition, HasWorldPosition = true });
        }

        /// <summary>Hush money for a customer who slipped on a spill, paid out of the till but never below zero.</summary>
        private void OnCustomerSlipped(string customerName, Vector3 at)
        {
            if (!_initialized) return;
            float bribe = Mathf.Clamp(config.SlipBribe, 0f, _balance);
            if (bribe <= 0f)
            {
                GameEvents.RaiseNotice($"{customerName} slipped. The till is empty, so all you could offer was an apology.");
                return;
            }

            _balance -= bribe;
            _expensesToday += bribe;
            GameEvents.RaiseNotice($"{customerName} slipped. You slid them ${bribe:0.00} to not call anyone.");
            Publish(new MoneyChange { Delta = -bribe, Balance = _balance, Reason = $"Hush money: {customerName}", WorldPosition = at, HasWorldPosition = true });
        }

        private void OnDineAndDashResolved(string customerName, bool caught, float amount)
        {
            if (!_initialized) return;
            _dineAndDashesToday++;
            if (caught)
            {
                // The money itself arrives as coins through MoneyRecovered.
                _dashersCaughtToday++;
            }
            else
            {
                _customersLost++;
                GameEvents.RaiseNotice($"{customerName} dined and dashed with ${amount:0.00}");
            }
            PublishSnapshot();
        }

        private void OnReputationChanged(float value, float delta, string reason)
        {
            _reputation = value;
        }

        private void OnDayStarted(int day)
        {
            if (!_initialized) return;
            ResetDayCounters();
            PublishSnapshot();
        }

        /// <summary>
        /// Closes the books: profit is revenue minus expenses; when positive, the player's share
        /// leaves the till and goes into their own cash, the store keeps the rest. Loss days pay nothing.
        /// </summary>
        private void OnDayEnded(int day)
        {
            if (!_initialized) return;

            float profit = _revenueToday - _expensesToday;
            float playerShare = 0f;
            float storeShare = 0f;
            if (profit > 0f)
            {
                playerShare = profit * (1f - Mathf.Clamp01(config.StoreShare));
                storeShare = profit - playerShare;
                _balance -= playerShare;
                _playerCash += playerShare;
            }

            var summary = new DaySummary
            {
                Day = day,
                Revenue = _revenueToday,
                Expenses = _expensesToday,
                Deductions = _deductionsToday,
                Profit = profit,
                StoreShare = storeShare,
                PlayerShare = playerShare,
                PlayerCashTotal = _playerCash,
                CustomersServed = _customersServed,
                CustomersLost = _customersLost,
                DineAndDashes = _dineAndDashesToday,
                DashersCaught = _dashersCaughtToday,
                Reputation = _reputation,
            };

            GameEvents.RaiseNotice(profit > 0f
                ? $"Day {day} closed with ${profit:0.00} profit. Your cut: ${playerShare:0.00}."
                : $"Day {day} closed with {(profit < 0f ? "-" : "")}${Mathf.Abs(profit):0.00} profit. No payout today.");
            GameEvents.RaiseDaySummaryReady(summary);

            ResetDayCounters();
            Publish(new MoneyChange { Delta = -playerShare, Balance = _balance, Reason = $"Your cut, day {day}" });
        }

        private void ResetDayCounters()
        {
            _revenueToday = 0f;
            _expensesToday = 0f;
            _deductionsToday = 0f;
            _customersServed = 0;
            _customersLost = 0;
            _dineAndDashesToday = 0;
            _dashersCaughtToday = 0;
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
                PlayerCash = _playerCash,
                RevenueToday = _revenueToday,
                ExpensesToday = _expensesToday,
                DeductionsToday = _deductionsToday,
                CustomersServed = _customersServed,
                CustomersLost = _customersLost,
                DineAndDashesToday = _dineAndDashesToday,
                DashersCaughtToday = _dashersCaughtToday,
            });
        }
    }
}
