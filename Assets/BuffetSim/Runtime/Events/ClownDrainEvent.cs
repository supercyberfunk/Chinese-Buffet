using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Customers;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A white-gloved hand comes up out of the shower drain under a table and takes whoever is
    /// sitting there. Nothing for the player to do: the bill pays itself and the table is clean.
    /// Sounds bad. It is the best thing that can happen to you.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Clown Drain", fileName = "ClownDrain")]
    public sealed class ClownDrainEvent : ChaosEvent
    {
        [SerializeField] private float riseSeconds = 2f;
        [SerializeField] private float dragSeconds = 1.6f;

        public float RiseSeconds => riseSeconds;
        public float DragSeconds => dragSeconds;

        public static ClownDrainEvent CreateDefault()
        {
            var e = CreateInstance<ClownDrainEvent>();
            e.Configure("ClownDrain", "Clown drain", "A hand came up out of a drain. It has a balloon.", 1f, 1, 200f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<ClownDrainRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="ClownDrainEvent"/>: find a drain with someone over it, rise, grab, gone.</summary>
    public sealed class ClownDrainRunner : ChaosEventRunner
    {
        private const float DrainMatchDistance = 0.6f;

        private ClownDrainEvent _event;
        private CustomerAgent _victim;
        private Transform _hand;
        private Vector3 _drain;
        private Vector3 _seat;
        private float _timer;
        private int _stage; // 0 rising, 1 reaching, 2 dragging down

        protected override void OnBegin()
        {
            _event = Definition as ClownDrainEvent;
            _victim = PickVictim(out _drain);
            if (_victim == null)
            {
                GameEvents.RaiseNotice("A gloved hand came up out of a drain, found nobody sitting there, and went back down. It honked once.");
                Finish(true, "Nobody was sitting over a drain");
                return;
            }

            _seat = _victim.transform.position;
            _victim.Hold("Hearing a honk from under the table");
            BuildHand();
            GameEvents.RaiseNotice($"Something is coming up the drain under {_victim.CustomerName}'s table. (audio cue: a single, distant honk)");
            _timer = _event != null ? _event.RiseSeconds : 2f;
        }

        /// <summary>A seated customer whose table sits on a drain, or null.</summary>
        private CustomerAgent PickVictim(out Vector3 drain)
        {
            IReadOnlyList<Vector3> drains = Ctx.GetDrainPoints();
            var candidates = new List<CustomerAgent>();
            var drainsFor = new List<Vector3>();
            IReadOnlyList<CustomerAgent> all = Ctx.GetCustomersInStore();
            for (int i = 0; i < all.Count; i++)
            {
                CustomerAgent customer = all[i];
                if (customer == null || !customer.IsSeated || customer.IsPaid || customer.IsHeld) continue;
                if (!customer.TryGetTablePosition(out Vector3 table)) continue;
                for (int d = 0; d < drains.Count; d++)
                {
                    if (ChaosActors.HorizontalDistance(table, drains[d]) > DrainMatchDistance) continue;
                    candidates.Add(customer);
                    drainsFor.Add(customer.transform.position);
                    break;
                }
            }

            if (candidates.Count == 0)
            {
                drain = Vector3.zero;
                return null;
            }
            int pick = Ctx.Rng.Next(candidates.Count);
            drain = new Vector3(drainsFor[pick].x, 0f, drainsFor[pick].z);
            return candidates[pick];
        }

        private void BuildHand()
        {
            _hand = new GameObject("Clown Hand").transform;
            _hand.SetParent(transform, false);
            _hand.position = _drain + Vector3.down * 1.2f;
            Material glove = MaterialLibrary.Get(Color.white);
            PrimitiveFactory.Visual("Palm", PrimitiveType.Sphere, _hand, new Vector3(0f, 0.9f, 0f), new Vector3(0.36f, 0.3f, 0.2f), glove);
            for (int f = 0; f < 4; f++)
                PrimitiveFactory.Visual($"Finger {f + 1}", PrimitiveType.Capsule, _hand, new Vector3(-0.12f + 0.08f * f, 1.12f, 0f), new Vector3(0.07f, 0.16f, 0.07f), glove);
            PrimitiveFactory.Visual("Thumb", PrimitiveType.Capsule, _hand, new Vector3(0.2f, 0.95f, 0f), new Vector3(0.07f, 0.12f, 0.07f), glove).transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
            PrimitiveFactory.Visual("Sleeve", PrimitiveType.Cylinder, _hand, new Vector3(0f, 0.35f, 0f), new Vector3(0.22f, 0.45f, 0.22f), MaterialLibrary.Get(new Color(0.9f, 0.2f, 0.25f)));
            PrimitiveFactory.Visual("Frill", PrimitiveType.Cylinder, _hand, new Vector3(0f, 0.72f, 0f), new Vector3(0.4f, 0.03f, 0.4f), MaterialLibrary.Get(new Color(1f, 0.85f, 0.2f)));
            PrimitiveFactory.Visual("Balloon String", PrimitiveType.Cube, _hand, new Vector3(0.1f, 1.6f, 0f), new Vector3(0.01f, 0.9f, 0.01f), MaterialLibrary.Get(Color.white));
            PrimitiveFactory.Visual("Balloon", PrimitiveType.Sphere, _hand, new Vector3(0.1f, 2.2f, 0f), Vector3.one * 0.35f, MaterialLibrary.Get(new Color(0.9f, 0.1f, 0.1f)));
        }

        private void Update()
        {
            if (IsFinished) return;
            if (_victim == null)
            {
                Finish(true, "The hand lost interest");
                return;
            }

            float dt = Time.deltaTime;
            _timer -= dt;
            float rise = _event != null ? _event.RiseSeconds : 2f;
            float drag = _event != null ? _event.DragSeconds : 1.6f;

            switch (_stage)
            {
                case 0:
                {
                    float t = 1f - Mathf.Clamp01(_timer / rise);
                    _hand.position = Vector3.Lerp(_drain + Vector3.down * 1.2f, _drain, Mathf.SmoothStep(0f, 1f, t));
                    if (_timer <= 0f)
                    {
                        _stage = 1;
                        _timer = 0.6f;
                        _victim.SetHoldStatus("...");
                    }
                    break;
                }
                case 1:
                {
                    float t = 1f - Mathf.Clamp01(_timer / 0.6f);
                    _hand.position = Vector3.Lerp(_drain, _seat + Vector3.up * 0.2f, t);
                    if (_timer <= 0f)
                    {
                        _stage = 2;
                        _timer = drag;
                        _victim.PayNow();
                        _victim.BeginScriptedMotion(); // the NavMeshAgent would keep lifting them back onto the floor
                        GameEvents.RaiseNotice($"The hand has {_victim.CustomerName}. {_victim.CustomerName}'s bill paid itself on the way down. (audio cue: three honks, descending)");
                    }
                    break;
                }
                case 2:
                {
                    float t = 1f - Mathf.Clamp01(_timer / drag);
                    Vector3 down = Vector3.Lerp(_seat, _drain + Vector3.down * 2.5f, t);
                    _hand.position = down + Vector3.up * 0.2f;
                    _victim.transform.position = down;
                    _victim.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, t);
                    if (_timer <= 0f)
                    {
                        string name = _victim.CustomerName;
                        _victim.Vanish();
                        _victim = null;
                        Finish(true, $"{name} went down the drain. Bill paid, table clean.");
                    }
                    break;
                }
            }
        }

        public override void Abort()
        {
            if (_victim != null)
            {
                _victim.transform.localScale = Vector3.one;
                _victim.EndScriptedMotion();
                _victim.ReleaseHold();
            }
            EndSilently();
        }
    }
}
