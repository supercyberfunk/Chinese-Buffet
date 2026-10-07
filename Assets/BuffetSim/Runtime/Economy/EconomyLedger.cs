using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Economy
{
    /// <summary>
    /// Owns the store's money and the player's own wallet. Nothing else mutates either: customers
    /// publish receipts, stations publish purchase requests, thieves publish theft requests, coins
    /// publish recoveries, fines arrive as expenses, and this component answers through the event
    /// bus. At the end of each day it splits the profit between the store and the player's wallet
    /// (the 60/40 from the notes). Bill rules from fortunes (comped, boosted, forgiven) live here too.
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

        // Bill rules (fortunes): comp the next N, boost the next N, forgive mistakes until a time.
        private int _compNext;
        private int _boostNext;
        private float _boostMultiplier = 1f;
        private float _forgiveUntil = float.NegativeInfinity;

        public float Balance => _balance;
        /// <summary>The player's own money: the daily cut plus anything picked up for themselves. Never spent by the store.</summary>
        public float PlayerCash => _playerCash;

        private bool MistakesForgiven => Time.time < _forgiveUntil;

        public void Initialize(EconomyConfig economyConfig)
        {
            config = economyConfig;
            _balance = config.StartingMoney;
            _playerCash = Mathf.Max(0f, config.StartingPlayerCash);
            _reputation = config.StartingReputation;
            _initialized = true;
            Publish(new MoneyChange { Delta = 0f, Balance = _balance, Reason = "Opening float" });
            GameEvents.RaiseWalletChanged(_playerCash, 0f, "Lunch money");
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
            GameEvents.WalletSpendRequested += OnWalletSpendRequested;
            GameEvents.WalletCredited += OnWalletCredited;
            GameEvents.ExpenseCharged += OnExpenseCharged;
            GameEvents.BillModifierRequested += OnBillModifierRequested;
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
            GameEvents.WalletSpendRequested -= OnWalletSpendRequested;
            GameEvents.WalletCredited -= OnWalletCredited;
            GameEvents.ExpenseCharged -= OnExpenseCharged;
            GameEvents.BillModifierRequested -= OnBillModifierRequested;
        }

        private void OnCustomerPaid(CustomerReceipt receipt)
        {
            if (!_initialized) return;
            _customersServed++;

            // Fortunes can rewrite the bill: forgiven mistakes drop the deductions, a comp zeroes it, a boost scales it.
            float total = receipt.Total;
            float deductions = receipt.Deductions;
            string rule = string.Empty;
            if (MistakesForgiven && deductions > 0f)
            {
                total = receipt.BaseAmount;
                deductions = 0f;
                rule = " (mistakes forgiven)";
            }
            if (_compNext > 0 && total > 0f)
            {
                _compNext--;
                GameEvents.RaiseNotice($"{receipt.CustomerName}'s ${total:0.00} bill is comped. Generosity is its own reward.");
                total = 0f;
                rule = " (comped)";
            }
            else if (_boostNext > 0 && total > 0f)
            {
                _boostNext--;
                total *= _boostMultiplier;
                rule = $" (x{_boostMultiplier:0.##}, everyone is hungry)";
            }

            _deductionsToday += deductions;

            if (total <= 0f)
            {
                if (rule.Length == 0)
                    GameEvents.RaiseNotice($"{receipt.CustomerName} left without paying: none of their {receipt.UnitsWanted} units could be served.");
                Publish(new MoneyChange { Delta = 0f, Balance = _balance, Reason = $"{receipt.CustomerName}: $0", WorldPosition = receipt.WorldPosition, HasWorldPosition = true });
                return;
            }

            _balance += total;
            _revenueToday += total;
            string detail = deductions > 0f
                ? $"{receipt.CustomerName} paid ${total:0.00} (${receipt.BaseAmount:0.00} minus ${deductions:0.00} for {receipt.UnitsWanted - receipt.UnitsTaken} missing units){rule}"
                : $"{receipt.CustomerName} paid ${total:0.00} in full{rule}";
            GameEvents.RaiseNotice(detail);
            Publish(new MoneyChange { Delta = total, Balance = _balance, Reason = receipt.CustomerName, WorldPosition = receipt.WorldPosition, HasWorldPosition = true });
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
            if (MistakesForgiven)
            {
                GameEvents.RaiseNotice($"{count} dish{(count == 1 ? "" : "es")} broken. Forgiven.");
                return;
            }

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
            float wanted = request.RequestedFraction > 0f ? _balance * request.RequestedFraction : request.RequestedAmount;
            float taken = Mathf.Clamp(wanted, 0f, _balance);
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

        /// <summary>Fines, the landlord, the plumber: the till pays what it can and the rest is written off.</summary>
        private void OnExpenseCharged(float amount, string reason, Vector3 at)
        {
            if (!_initialized || amount <= 0f) return;
            float paid = Mathf.Min(amount, _balance);
            _balance -= paid;
            _expensesToday += paid;
            GameEvents.RaiseNotice(paid < amount
                ? $"{reason}: -${amount:0.00} (the till only had ${paid:0.00})"
                : $"{reason}: -${amount:0.00}");
            Publish(new MoneyChange { Delta = -paid, Balance = _balance, Reason = reason, WorldPosition = at, HasWorldPosition = true });
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

        /// <summary>The player's own cash pays for the slot machine; the till is never touched.</summary>
        private void OnWalletSpendRequested(WalletSpendRequest request)
        {
            if (!_initialized || request == null || request.Approved) return;
            if (request.Amount > _playerCash + 0.0001f)
            {
                GameEvents.RaiseNotice($"Your wallet has ${_playerCash:0.00}; {request.Reason} costs ${request.Amount:0.00}.");
                return;
            }

            _playerCash = Mathf.Max(0f, _playerCash - request.Amount);
            request.Approved = true;
            GameEvents.RaiseWalletChanged(_playerCash, -request.Amount, request.Reason);
            PublishSnapshot();
        }

        /// <summary>Money for the player alone (slot payouts, fountain coins). Outside the split and the day's numbers.</summary>
        private void OnWalletCredited(float amount, string reason, Vector3 at)
        {
            if (!_initialized || amount <= 0f) return;
            _playerCash += amount;
            GameEvents.RaiseWalletChanged(_playerCash, amount, reason);
            PublishSnapshot();
        }

        private void OnBillModifierRequested(BillModifier modifier)
        {
            switch (modifier.Kind)
            {
                case BillModifierKind.CompNext:
                    _compNext += Mathf.Max(1, modifier.Count);
                    break;
                case BillModifierKind.BoostNext:
                    _boostNext += Mathf.Max(1, modifier.Count);
                    _boostMultiplier = modifier.Multiplier > 0f ? modifier.Multiplier : 1.25f;
                    break;
                case BillModifierKind.ForgiveMistakes:
                    _forgiveUntil = Mathf.Max(_forgiveUntil, Time.time) + Mathf.Max(0f, modifier.Seconds);
                    break;
            }
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
            if (playerShare > 0f) GameEvents.RaiseWalletChanged(_playerCash, playerShare, $"Your cut, day {day}");
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
