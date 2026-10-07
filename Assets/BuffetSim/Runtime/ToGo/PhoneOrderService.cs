using System.Collections.Generic;
using System.Text;
using BuffetSim.Core;
using BuffetSim.Economy;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.ToGo
{
    /// <summary>
    /// The wall phone by the register and the one phone order it can hold at a time. It rings a few
    /// times a day (more as satisfaction climbs); answer it (E) and the caller reads an order of two
    /// to four foods, five units at most, that stays on screen only while they talk. Pack a to-go
    /// box from the buffet and put it on the pickup shelf; the shelf asks this service, over the
    /// bus, whether the box matches, and the receipt goes through the ledger like any customer.
    /// </summary>
    public sealed class PhoneOrderService : MonoBehaviour, IInteractable
    {
        private sealed class Order
        {
            public int Id;
            public string Caller;
            public readonly List<FoodDefinition> Foods = new List<FoodDefinition>();
            public readonly List<int> Units = new List<int>();
            public string Script;
            public int TotalUnits;
            public float SecondsAllowed;
            public float SecondsLeft;
            public float OnLineLeft;
        }

        private static readonly string[] Callers =
        {
            "Deb", "Marcus", "Tran", "Big Lou", "Kayleigh", "Mr. Okafor", "The dentist's office", "Pastor Rick",
            "Jenna (no last name)", "Someone's grandma", "The tire place", "Craig from the vape shop", "A kid with a credit card",
        };
        private static readonly string[] Openers =
        {
            "Yeah hi, is this the buffet? Lemme get",
            "Hi, pickup order. I want",
            "(chewing) Hey. Can I do",
            "Okay so my husband wants, hang on,",
            "It's me again. Same as last time, which was",
            "Hello? HELLO? Okay. I need",
            "Hi, do you guys do takeout? Great. So",
        };
        private static readonly string[] Closers =
        {
            "That's it. Fifteen minutes? Great. Bye.",
            "No sauce. Actually sauce. Okay bye.",
            "Put it on the shelf, I'll grab it. (click)",
            "How much is... never mind. Bye.",
            "And I'm in a hurry. (dial tone)",
            "You got all that? Don't write it down, you'll remember. Bye.",
            "Thanks hon. (hangs up mid-word)",
        };

        [SerializeField] private EconomyConfig config;
        [SerializeField] private FoodCatalog foods;
        [SerializeField] private TextMesh label;
        [SerializeField] private Transform handset;

        private System.Random _rng = new System.Random();
        private DayPhase _phase = DayPhase.Open;
        private int _day = 1;
        private float _reputation = 50f;
        private int _callsToday;
        private int _callsAllowed;
        private float _nextCallIn = float.PositiveInfinity;
        private bool _ringing;
        private float _ringLeft;
        private Order _order;
        private int _nextId = 1;
        private float _tickTimer;
        private Quaternion _handsetRest = Quaternion.identity;

        public bool IsRinging => _ringing;
        public bool HasOrder => _order != null;

        private void OnEnable()
        {
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
            GameEvents.ReputationChanged += OnReputationChanged;
            GameEvents.PhoneRingRequested += OnRingRequested;
            GameEvents.ToGoDeliveryRequested += OnDeliveryRequested;
        }

        private void OnDisable()
        {
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
            GameEvents.ReputationChanged -= OnReputationChanged;
            GameEvents.PhoneRingRequested -= OnRingRequested;
            GameEvents.ToGoDeliveryRequested -= OnDeliveryRequested;
        }

        public void Initialize(EconomyConfig economyConfig, FoodCatalog catalog, System.Random rng, TextMesh statusLabel, Transform handsetVisual)
        {
            config = economyConfig;
            foods = catalog;
            _rng = rng ?? new System.Random();
            label = statusLabel;
            handset = handsetVisual;
            if (handset != null) _handsetRest = handset.localRotation;
            _callsAllowed = CallsAllowedFor(1);
            _nextCallIn = config != null ? config.PhoneFirstCallSeconds : 60f;
            RefreshLabel();
        }

        // ----- The E key -----

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_ringing) return "[E] Answer the phone";
            if (_order != null)
                return $"Phone: {_order.Caller}'s order, {Mathf.CeilToInt(_order.SecondsLeft)}s left{(_order.OnLineLeft > 0f ? " (still on the line)" : string.Empty)}";
            if (config != null && !config.PhoneOrdersEnabled) return "Phone (unplugged)";
            return _callsToday >= _callsAllowed ? "Phone (quiet for today)" : "Phone (quiet)";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_ringing)
            {
                Answer();
                return;
            }
            if (_order != null)
            {
                GameEvents.RaiseNotice("You pick up. Dial tone. You can't call them back; it's from memory now.");
                return;
            }
            GameEvents.RaiseNotice("Dial tone. Nobody is calling, which is its own kind of insult.");
        }

        // ----- Time -----

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_ringing && handset != null)
                handset.localRotation = _handsetRest * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 45f) * 10f);

            if (_phase == DayPhase.Closing || _phase == DayPhase.Closed) return;

            if (_order != null) TickOrder(dt);
            else if (_ringing) TickRing(dt);
            else TickGap(dt);
        }

        private void TickGap(float dt)
        {
            if (config == null || !config.PhoneOrdersEnabled || _callsToday >= _callsAllowed) return;
            _nextCallIn -= dt;
            if (_nextCallIn <= 0f) StartRinging(true);
        }

        private void TickRing(float dt)
        {
            _ringLeft -= dt;
            if (_ringLeft > 0f) return;
            _ringing = false;
            if (handset != null) handset.localRotation = _handsetRest;
            GameEvents.RaisePhoneRingingChanged(false);
            GameEvents.RaiseReputationNudged(config != null ? config.PhoneMissedReputation : -1f, "a phone nobody answered");
            GameEvents.RaiseNotice("The phone stopped ringing. Somebody is eating somewhere else tonight.");
            ScheduleNext();
            RefreshLabel();
        }

        private void TickOrder(float dt)
        {
            Order order = _order;
            bool wasOnLine = order.OnLineLeft > 0f;
            order.OnLineLeft -= dt;
            order.SecondsLeft -= dt;
            if (wasOnLine && order.OnLineLeft <= 0f)
            {
                GameEvents.RaiseNotice($"{order.Caller} hung up. The order is in your head now.");
                GameEvents.RaiseToGoOrderTicked(Info(order));
            }

            _tickTimer -= dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = 1f;
                GameEvents.RaiseToGoOrderTicked(Info(order));
                RefreshLabel();
            }

            if (order.SecondsLeft <= 0f) Expire();
        }

        private void StartRinging(bool countsTowardToday)
        {
            _ringing = true;
            _ringLeft = config != null ? config.PhoneRingSeconds : 20f;
            if (countsTowardToday) _callsToday++;
            GameEvents.RaisePhoneRingingChanged(true);
            GameEvents.RaiseNotice("The phone is ringing at the front counter. (audio cue: a wall phone from 1994, too loud)");
            RefreshLabel();
        }

        private void Answer()
        {
            _ringing = false;
            if (handset != null) handset.localRotation = _handsetRest;
            GameEvents.RaisePhoneRingingChanged(false);
            _order = Generate();
            if (_order == null)
            {
                GameEvents.RaiseNotice("\"Yeah, do you have... no? Okay.\" (click) The menu is too short to order from.");
                ScheduleNext();
                RefreshLabel();
                return;
            }
            _tickTimer = 1f;
            GameEvents.RaiseToGoOrderPlaced(Info(_order));
            GameEvents.RaiseNotice($"{_order.Caller}: {_order.Script}");
            RefreshLabel();
        }

        private Order Generate()
        {
            IReadOnlyList<FoodDefinition> menu = foods != null ? foods.Unlocked : null;
            if (menu == null || menu.Count == 0) return null;

            int minItems = config != null ? config.PhoneOrderMinItems : 2;
            int maxItems = config != null ? config.PhoneOrderMaxItems : 4;
            int maxUnits = config != null ? config.PhoneOrderMaxUnits : 5;
            int perItemCap = config != null ? Mathf.Max(1, config.ToGoBoxMaxPerItem) : 3;
            int itemCount = Mathf.Clamp(_rng.Next(minItems, maxItems + 1), 1, menu.Count);

            var indices = new List<int>(menu.Count);
            for (int i = 0; i < menu.Count; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var order = new Order
            {
                Id = _nextId++,
                Caller = Callers[_rng.Next(Callers.Length)],
                SecondsAllowed = config != null ? config.PhoneOrderSeconds : 150f,
            };
            order.SecondsLeft = order.SecondsAllowed;
            order.OnLineLeft = config != null ? config.PhoneReadSeconds : 10f;

            for (int i = 0; i < itemCount; i++)
            {
                order.Foods.Add(menu[indices[i]]);
                order.Units.Add(1);
            }
            int total = Mathf.Clamp(_rng.Next(itemCount, maxUnits + 1), itemCount, Mathf.Max(itemCount, itemCount * perItemCap));
            for (int extra = total - itemCount; extra > 0; extra--)
            {
                // Hand the spare units out at random, never past the per-item cap the box can hold.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int slot = _rng.Next(itemCount);
                    if (order.Units[slot] >= perItemCap) continue;
                    order.Units[slot]++;
                    break;
                }
            }
            order.TotalUnits = 0;
            for (int i = 0; i < order.Units.Count; i++) order.TotalUnits += order.Units[i];
            order.Script = BuildScript(order);
            return order;
        }

        private string BuildScript(Order order)
        {
            var sb = new StringBuilder();
            sb.Append('"').Append(Openers[_rng.Next(Openers.Length)]).Append(' ');
            for (int i = 0; i < order.Foods.Count; i++)
            {
                if (i > 0) sb.Append(i == order.Foods.Count - 1 ? " and " : ", ");
                sb.Append(Words(order.Units[i])).Append(' ').Append(order.Foods[i].DisplayName);
            }
            sb.Append(". ").Append(Closers[_rng.Next(Closers.Length)]).Append('"');
            return sb.ToString();
        }

        private static string Words(int n)
        {
            switch (n)
            {
                case 1: return "one";
                case 2: return "two";
                case 3: return "three";
                case 4: return "four";
                case 5: return "five";
                default: return n.ToString();
            }
        }

        private void Expire()
        {
            Order order = _order;
            _order = null;
            GameEvents.RaiseToGoOrderEnded(Info(order), false, "expired");
            GameEvents.RaiseReputationNudged(config != null ? config.PhoneExpiredReputation : -3f, $"{order.Caller}'s order never got racked");
            GameEvents.RaiseNotice($"{order.Caller} called back, said several things, and cancelled the order.");
            ScheduleNext();
            RefreshLabel();
        }

        private void ScheduleNext()
        {
            float min = config != null ? config.PhoneCallGapMin : 45f;
            float max = config != null ? config.PhoneCallGapMax : 90f;
            float gap = Mathf.Lerp(Mathf.Max(min, max), Mathf.Min(min, max), Mathf.Clamp01(_reputation / 100f));
            _nextCallIn = gap * (0.85f + 0.3f * (float)_rng.NextDouble());
        }

        private int CallsAllowedFor(int day)
        {
            if (config == null) return 3;
            return Mathf.Clamp(config.PhoneCallsDayOne + (day - 1) * config.PhoneCallsAddedPerDay, 0, config.PhoneCallsMax);
        }

        // ----- The pickup shelf asks whether a box matches -----

        private void OnDeliveryRequested(ToGoDeliveryRequest request)
        {
            if (request == null || request.Accepted) return;
            if (_order == null)
            {
                request.Outcome = "Nobody has ordered anything. Trash it or eat it.";
                return;
            }

            Order order = _order;
            int matched = 0;
            int missing = 0;
            var missingParts = new List<string>();
            for (int i = 0; i < order.Foods.Count; i++)
            {
                int have = request.CountOf(order.Foods[i]);
                int wanted = order.Units[i];
                int got = Mathf.Min(have, wanted);
                matched += got;
                if (got < wanted)
                {
                    missing += wanted - got;
                    missingParts.Add($"{wanted - got} {order.Foods[i].DisplayName}");
                }
            }
            int extras = Mathf.Max(0, request.TotalUnits - matched);

            // A box with none of their order in it is not a delivery, the same way a diner who got
            // nothing pays nothing: the order stays open and the box stays in your hands.
            if (matched == 0)
            {
                request.Outcome = request.TotalUnits > 0
                    ? $"{order.Caller} opened the box on the shelf, found none of their order in it and left it with you. Repack it."
                    : $"{order.Caller} is not paying for an empty box. Fill it first.";
                return;
            }

            float unitPrice = config != null ? config.ToGoUnitPrice : 3f;
            float deductionPerUnit = config != null ? config.UnfulfilledUnitDeduction : 1.25f;
            float baseAmount = order.TotalUnits * unitPrice;
            float deductions = missing * deductionPerUnit;
            float total = Mathf.Max(0f, baseAmount - deductions);

            _order = null;
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = $"{order.Caller} (to-go)",
                BaseAmount = baseAmount,
                Deductions = deductions,
                UnitsWanted = order.TotalUnits,
                UnitsTaken = matched,
                Total = total,
                WorldPosition = request.WorldPosition,
            });

            string outcome;
            // Satisfaction follows the receipt, like any other customer's.
            if (missing == 0)
            {
                outcome = extras > 0
                    ? $"Racked {order.Caller}'s order: all {order.TotalUnits} units, ${total:0.00}. The {extras} extra went free; they will not say thank you."
                    : $"Racked {order.Caller}'s order: all {order.TotalUnits} units, ${total:0.00}.";
            }
            else
            {
                outcome = $"Racked {order.Caller}'s order short: {matched}/{order.TotalUnits} units, ${total:0.00} (missing {string.Join(", ", missingParts)}).";
            }
            GameEvents.RaiseToGoOrderEnded(Info(order), true, missing == 0 ? "delivered" : "delivered short");
            GameEvents.RaiseNotice(outcome);
            request.Accepted = true;
            request.Outcome = outcome;
            ScheduleNext();
            RefreshLabel();
        }

        // ----- Bus -----

        private void OnDayStarted(int day)
        {
            _day = day;
            _callsToday = 0;
            _callsAllowed = CallsAllowedFor(day);
            _nextCallIn = config != null ? config.PhoneFirstCallSeconds : 60f;
            _phase = DayPhase.Open;
            RefreshLabel();
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            _phase = snapshot.Phase;
            if (_phase != DayPhase.Closing && _phase != DayPhase.Closed) return;

            if (_ringing)
            {
                _ringing = false;
                if (handset != null) handset.localRotation = _handsetRest;
                GameEvents.RaisePhoneRingingChanged(false);
            }
            if (_order != null)
            {
                Order order = _order;
                _order = null;
                GameEvents.RaiseToGoOrderEnded(Info(order), false, "closed");
                GameEvents.RaiseNotice($"Closing: {order.Caller}'s order is cancelled, no hard feelings.");
            }
            RefreshLabel();
        }

        private void OnReputationChanged(float value, float delta, string reason)
        {
            _reputation = value;
        }

        /// <summary>A fortune or the debug keys can make it ring; those calls don't count against the day.</summary>
        private void OnRingRequested()
        {
            if (_ringing || _order != null) return;
            if (_phase == DayPhase.Closing || _phase == DayPhase.Closed)
            {
                GameEvents.RaiseNotice("The phone rang once after close and thought better of it.");
                return;
            }
            StartRinging(false);
        }

        private ToGoOrderInfo Info(Order order)
        {
            return new ToGoOrderInfo
            {
                Id = order.Id,
                CallerName = order.Caller,
                Script = order.Script,
                TotalUnits = order.TotalUnits,
                ItemCount = order.Foods.Count,
                SecondsAllowed = order.SecondsAllowed,
                SecondsLeft = Mathf.Max(0f, order.SecondsLeft),
                OnTheLine = order.OnLineLeft > 0f,
            };
        }

        private void RefreshLabel()
        {
            if (label == null) return;
            if (_ringing) label.text = "PHONE\nRINGING";
            else if (_order != null) label.text = $"PHONE\n{_order.Caller}: {Mathf.CeilToInt(_order.SecondsLeft)}s";
            else label.text = "PHONE";
        }
    }
}
