using BuffetSim.Core;
using BuffetSim.Economy;
using UnityEngine;

namespace BuffetSim.Day
{
    /// <summary>
    /// Runs the business day as a small phase machine: Open, LastCall (the final minute), Closing
    /// (doors shut, stragglers finish or the grace period runs out), Closed (summary up), then the
    /// next day. It only publishes time on the event bus; the spawner, ledger and HUD react to it.
    /// It never touches customers or money itself.
    /// </summary>
    public sealed class DayClock : MonoBehaviour
    {
        private const float TickInterval = 0.25f;

        [SerializeField] private EconomyConfig config;

        private int _day;
        private DayPhase _phase;
        private float _secondsRemaining;
        private float _phaseTimer;
        private float _tickTimer;
        private int _customersInStore;
        private bool _running;

        public int Day => _day;
        public DayPhase Phase => _phase;
        public float SecondsRemaining => _secondsRemaining;

        /// <summary>Starts day 1 immediately: raises DayStarted(1) and then DayPhaseChanged(Open).</summary>
        public void Initialize(EconomyConfig economyConfig)
        {
            config = economyConfig;
            _customersInStore = 0;
            _tickTimer = 0f;
            _running = true;
            StartDay(1);
        }

        private void OnEnable()
        {
            GameEvents.CustomerCountChanged += OnCustomerCountChanged;
            GameEvents.DayFastForwardRequested += OnFastForwardRequested;
        }

        private void OnDisable()
        {
            GameEvents.CustomerCountChanged -= OnCustomerCountChanged;
            GameEvents.DayFastForwardRequested -= OnFastForwardRequested;
        }

        /// <summary>Debug: skip ahead. The phase machine in Update notices the new reading on its next tick.</summary>
        private void OnFastForwardRequested(float seconds)
        {
            if (!_running || seconds <= 0f) return;
            if (_phase == DayPhase.Open || _phase == DayPhase.LastCall) _secondsRemaining = Mathf.Max(0f, _secondsRemaining - seconds);
            else _phaseTimer -= seconds;
            GameEvents.RaiseDayClockTicked(Snapshot());
        }

        private void Update()
        {
            if (!_running || config == null) return;
            float dt = Time.deltaTime;

            switch (_phase)
            {
                case DayPhase.Open:
                    _secondsRemaining = Mathf.Max(0f, _secondsRemaining - dt);
                    if (_secondsRemaining <= 0f)
                        SetPhase(DayPhase.Closing);
                    else if (_secondsRemaining <= config.LastCallSeconds)
                        SetPhase(DayPhase.LastCall);
                    break;

                case DayPhase.LastCall:
                    _secondsRemaining = Mathf.Max(0f, _secondsRemaining - dt);
                    if (_secondsRemaining <= 0f) SetPhase(DayPhase.Closing);
                    break;

                case DayPhase.Closing:
                    _phaseTimer -= dt;
                    if (_customersInStore <= 0 || _phaseTimer <= 0f) SetPhase(DayPhase.Closed);
                    break;

                case DayPhase.Closed:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) StartDay(_day + 1);
                    break;
            }

            _tickTimer -= dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = TickInterval;
                GameEvents.RaiseDayClockTicked(Snapshot());
            }
        }

        private void StartDay(int day)
        {
            _day = day;
            _secondsRemaining = Mathf.Max(0f, config.DayLengthSeconds);
            _phaseTimer = 0f;
            GameEvents.RaiseDayStarted(_day);
            SetPhase(DayPhase.Open);
        }

        private void SetPhase(DayPhase next)
        {
            _phase = next;
            switch (next)
            {
                case DayPhase.Open:
                    GameEvents.RaiseNotice($"Day {_day}. Doors open.");
                    break;
                case DayPhase.LastCall:
                    GameEvents.RaiseNotice("Last call.");
                    break;
                case DayPhase.Closing:
                    _secondsRemaining = 0f;
                    _phaseTimer = Mathf.Max(0f, config.ClosingGraceSeconds);
                    GameEvents.RaiseNotice("Closing time. Waiting for the last tables to clear.");
                    break;
                case DayPhase.Closed:
                    _secondsRemaining = 0f;
                    _phaseTimer = Mathf.Max(0f, config.SummarySeconds);
                    GameEvents.RaiseNotice("Closed. Lights off.");
                    break;
            }

            GameEvents.RaiseDayPhaseChanged(Snapshot());

            // The doors are shut and everyone has vanished with the lights; the ledger answers with the summary.
            if (next == DayPhase.Closed) GameEvents.RaiseDayEnded(_day);
        }

        private void OnCustomerCountChanged(int inStore)
        {
            _customersInStore = inStore;
        }

        private DayClockSnapshot Snapshot()
        {
            return new DayClockSnapshot
            {
                Day = _day,
                Phase = _phase,
                SecondsRemaining = _secondsRemaining,
                DayLengthSeconds = config != null ? config.DayLengthSeconds : 0f,
            };
        }
    }
}
