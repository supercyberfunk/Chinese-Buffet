using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Economy;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// Rolls for a chaos event every EventCheckInterval seconds while the doors are open and someone
    /// is in the building, picks a weighted random eligible definition from the catalog and runs at
    /// most one scheduled event at a time. Events asked for by id on the bus (fortunes, the robbery
    /// counter, the debug keys) run alongside it, outside the daily cap. Day boundaries come off the
    /// bus; everything running is aborted when the lights go off. Without a DayClock it behaves as
    /// if day 1 were open forever.
    /// </summary>
    public sealed class ChaosEventScheduler : MonoBehaviour
    {
        [SerializeField] private ChaosEventCatalog catalog;

        private readonly Dictionary<ChaosEvent, float> _endedAt = new Dictionary<ChaosEvent, float>();
        private readonly List<ChaosEvent> _eligible = new List<ChaosEvent>();
        private readonly List<ChaosEventRunner> _extras = new List<ChaosEventRunner>();

        private ChaosEventContext _ctx;
        private EconomyConfig _config;
        private ChaosEventRunner _active;
        private ChaosEvent _activeDefinition;
        private DayPhase _phase = DayPhase.Open;
        private int _day = 1;
        private int _eventsToday;
        private int _customersInStore;
        private float _dayStartedAt;
        private float _lastEventEndedAt = float.NegativeInfinity;
        private float _checkTimer;
        private bool _ready;

        public ChaosEventCatalog Catalog => catalog;
        /// <summary>The runner currently in progress, or null.</summary>
        public ChaosEventRunner ActiveRunner => _active != null && !_active.IsFinished ? _active : null;
        public bool HasActiveEvent => _activeDefinition != null;
        /// <summary>Requested events currently running next to the scheduled one.</summary>
        public int ExtraCount => _extras.Count;
        public int EventsToday => _eventsToday;
        public int Day => _day;

        public void Initialize(ChaosEventCatalog eventCatalog, ChaosEventContext context)
        {
            catalog = eventCatalog;
            _ctx = context;
            _config = context != null ? context.Config : null;
            _dayStartedAt = Time.time;
            _checkTimer = _config != null ? _config.EventCheckInterval : 40f;
            _ready = catalog != null && _ctx != null && _config != null;
            if (!_ready) Debug.LogWarning("[Buffet] ChaosEventScheduler needs a catalog and a context with an EconomyConfig; no events will run.");
        }

        private void OnEnable()
        {
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
            GameEvents.CustomerCountChanged += OnCustomerCountChanged;
            GameEvents.ChaosEventRequested += OnEventRequested;
        }

        private void OnDisable()
        {
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
            GameEvents.CustomerCountChanged -= OnCustomerCountChanged;
            GameEvents.ChaosEventRequested -= OnEventRequested;
        }

        private void Update()
        {
            if (!_ready) return;
            TrackActive();
            PruneExtras();
            if (_phase != DayPhase.Open) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = Mathf.Max(1f, _config.EventCheckInterval);
            TryStartRandomEvent();
        }

        /// <summary>Starts <paramref name="definition"/> right now, aborting whatever is running (testing and debug keys).</summary>
        public ChaosEventRunner ForceStart(ChaosEvent definition)
        {
            if (!_ready || definition == null) return null;
            AbortActive();
            return StartEvent(definition);
        }

        /// <summary>
        /// An event asked for by id: it runs on top of whatever is scheduled, doesn't count against
        /// the daily cap, and is skipped silently once the doors have closed. Unknown ids are logged.
        /// </summary>
        private void OnEventRequested(string id)
        {
            if (!_ready || string.IsNullOrEmpty(id)) return;
            if (_phase == DayPhase.Closed) return;
            ChaosEvent definition = catalog.Find(id);
            if (definition == null)
            {
                Debug.LogWarning($"[Buffet] No chaos event with id '{id}' in the catalog.");
                return;
            }
            ChaosEventRunner runner = definition.Begin(_ctx, transform);
            if (runner == null || runner.IsFinished) return;
            _extras.Add(runner);
            _endedAt[definition] = Time.time; // keeps the random roll from picking the same thing right away
        }

        private void PruneExtras()
        {
            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                if (_extras[i] == null || _extras[i].IsFinished) _extras.RemoveAt(i);
            }
        }

        private void AbortExtras()
        {
            for (int i = 0; i < _extras.Count; i++)
            {
                ChaosEventRunner runner = _extras[i];
                if (runner == null || runner.IsFinished) continue;
                runner.Abort();
                if (runner != null) Destroy(runner.gameObject);
            }
            _extras.Clear();
        }

        private void TryStartRandomEvent()
        {
            if (_activeDefinition != null) return;
            if (_customersInStore <= 0) return;
            if (_eventsToday >= _config.MaxEventsPerDay) return;
            if (Time.time - _lastEventEndedAt < _config.EventMinGap) return;
            if (Time.time - _dayStartedAt < _config.EventQuietStart) return;
            if (_ctx.Rng.NextDouble() >= _config.EventChance) return;

            ChaosEvent pick = PickWeighted();
            if (pick != null) StartEvent(pick);
        }

        /// <summary>Weighted random over the events allowed today whose cooldown has elapsed.</summary>
        private ChaosEvent PickWeighted()
        {
            _eligible.Clear();
            float total = 0f;
            IReadOnlyList<ChaosEvent> all = catalog.Events;
            for (int i = 0; i < all.Count; i++)
            {
                ChaosEvent candidate = all[i];
                if (candidate == null || candidate.Weight <= 0f || candidate.MinDay > _day) continue;
                if (_endedAt.TryGetValue(candidate, out float endedAt) && Time.time - endedAt < candidate.CooldownSeconds) continue;
                _eligible.Add(candidate);
                total += candidate.Weight;
            }
            if (_eligible.Count == 0 || total <= 0f) return null;

            float roll = (float)_ctx.Rng.NextDouble() * total;
            for (int i = 0; i < _eligible.Count; i++)
            {
                roll -= _eligible[i].Weight;
                if (roll <= 0f) return _eligible[i];
            }
            return _eligible[_eligible.Count - 1];
        }

        private ChaosEventRunner StartEvent(ChaosEvent definition)
        {
            ChaosEventRunner runner = definition.Begin(_ctx, transform);
            if (runner == null) return null;
            _active = runner;
            _activeDefinition = definition;
            _eventsToday++;
            if (runner.IsFinished) RecordEnded(); // some events resolve inside OnBegin
            return runner;
        }

        /// <summary>Notices a runner that finished (or vanished) and starts its cooldown.</summary>
        private void TrackActive()
        {
            if (_activeDefinition == null) return;
            if (_active == null || _active.IsFinished) RecordEnded();
        }

        private void RecordEnded()
        {
            if (_activeDefinition != null) _endedAt[_activeDefinition] = Time.time;
            _lastEventEndedAt = Time.time;
            _activeDefinition = null;
            _active = null;
        }

        private void AbortActive()
        {
            if (_activeDefinition == null) return;
            if (_active != null && !_active.IsFinished)
            {
                _active.Abort();
                // Belt and braces: the runner's own Abort should have done this already; a second Destroy is harmless.
                if (_active != null) Destroy(_active.gameObject);
            }
            RecordEnded();
        }

        private void OnDayStarted(int day)
        {
            _day = day;
            _eventsToday = 0;
            _dayStartedAt = Time.time;
            _checkTimer = _config != null ? _config.EventCheckInterval : 40f;
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            _phase = snapshot.Phase;
            _day = snapshot.Day;
            if (_phase == DayPhase.Closed)
            {
                AbortActive();
                AbortExtras();
            }
        }

        private void OnCustomerCountChanged(int inStore)
        {
            _customersInStore = inStore;
        }
    }
}
