using BuffetSim.Core;
using BuffetSim.Economy;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// The robbery table from the notes, separate from the event roll: every 15-20 paying customers
    /// there is a 25% chance of a robbery, at most one a day, and never before the first batch has
    /// paid. It only counts receipts and asks for the "Robbery" event by id on the bus.
    /// </summary>
    public sealed class RobberyScheduler : MonoBehaviour
    {
        public const string EventId = "Robbery";

        [SerializeField] private EconomyConfig config;

        private System.Random _rng;
        private int _receiptsSinceRoll;
        private int _nextRollAt;
        private int _robberiesToday;
        private DayPhase _phase = DayPhase.Open;
        private bool _ready;

        public int ReceiptsUntilNextRoll => Mathf.Max(0, _nextRollAt - _receiptsSinceRoll);

        public void Initialize(EconomyConfig economyConfig, System.Random rng)
        {
            config = economyConfig;
            _rng = rng ?? new System.Random();
            _ready = config != null;
            ScheduleNextRoll();
        }

        private void OnEnable()
        {
            GameEvents.CustomerPaid += OnCustomerPaid;
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
        }

        private void OnDisable()
        {
            GameEvents.CustomerPaid -= OnCustomerPaid;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
        }

        private void OnCustomerPaid(CustomerReceipt receipt)
        {
            if (!_ready || _phase != DayPhase.Open) return;
            _receiptsSinceRoll++;
            if (_receiptsSinceRoll < _nextRollAt) return;

            _receiptsSinceRoll = 0;
            ScheduleNextRoll();
            if (_robberiesToday >= config.RobberiesPerDay) return;
            if (_rng.NextDouble() >= config.RobberyChance) return;

            _robberiesToday++;
            GameEvents.RaiseChaosEventRequested(EventId);
        }

        private void ScheduleNextRoll()
        {
            if (!_ready) return;
            int min = Mathf.Max(1, config.RobberyEveryMinCustomers);
            int max = Mathf.Max(min, config.RobberyEveryMaxCustomers);
            _nextRollAt = _rng.Next(min, max + 1);
        }

        private void OnDayStarted(int day)
        {
            _robberiesToday = 0;
            _receiptsSinceRoll = 0;
            ScheduleNextRoll();
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            _phase = snapshot.Phase;
        }
    }
}
