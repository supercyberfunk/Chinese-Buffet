using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Tables;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A positive fortune ("You will not be alone."): a cousin in a visor walks in, clears the
    /// dirtiest table, carries the stack to the dishwasher and goes back for the next one, for
    /// three minutes. He does not speak, he does not sit, and he leaves the way he came. Weight
    /// zero: the scheduler never rolls him, he only turns up when asked for by id.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/The Cousin", fileName = "Cousin")]
    public sealed class CousinEvent : ChaosEvent
    {
        [SerializeField] private float stayingSeconds = 180f;
        [SerializeField] private float speed = 3.2f;
        [Tooltip("Seconds spent standing at a table before the plates come off it.")]
        [SerializeField] private float clearSeconds = 1.5f;

        public float StayingSeconds => stayingSeconds;
        public float Speed => speed;
        public float ClearSeconds => clearSeconds;

        public static CousinEvent CreateDefault()
        {
            var e = CreateInstance<CousinEvent>();
            e.Configure("Cousin", "The cousin", "A cousin in a visor has walked in and started clearing tables. Nobody asked.", 0f, 1, 60f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<CousinRunner>(context, parent);
        }
    }

    /// <summary>
    /// Runner for <see cref="CousinEvent"/>: table, dishwasher, table, dishwasher, door. Tables are
    /// found once on arrival; plates reach the dishwasher through the bus only, never by reference.
    /// </summary>
    public sealed class CousinRunner : ChaosEventRunner
    {
        private const float RecheckSeconds = 3f;
        private const float PlateThickness = 0.03f;
        private const float HandHeight = 1.05f;
        private const float HandForward = 0.45f;

        private static readonly Color PoloColor = new Color(0.2f, 0.45f, 0.7f);
        private static readonly Color HeadColor = new Color(0.82f, 0.68f, 0.5f);
        private static readonly Color VisorColor = new Color(1f, 0.35f, 0.1f);
        private static readonly Color PlateColor = new Color(0.92f, 0.92f, 0.9f);
        private static readonly Color LabelColor = new Color(0.8f, 0.95f, 1f);

        private enum Phase
        {
            Idle,
            ToTable,
            Clearing,
            ToDishwasher,
            Leaving,
        }

        private CousinEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private TextMesh _label;
        private GameObject _stack;
        private DiningTable[] _tables;
        private DiningTable _target;
        private Phase _phase;
        private float _speed;
        private float _clearSeconds;
        private float _stayTimer;
        private float _clearTimer;
        private float _recheckTimer;
        private int _carrying;
        private int _total;
        private bool _timeUp;

        protected override void OnBegin()
        {
            _event = Definition as CousinEvent;
            _speed = _event != null ? Mathf.Max(0.5f, _event.Speed) : 3.2f;
            _clearSeconds = _event != null ? Mathf.Max(0f, _event.ClearSeconds) : 1.5f;
            _stayTimer = _event != null ? _event.StayingSeconds : 180f;

            _figure = EventActor.BuildPerson("The Cousin", transform, Ctx, Ctx.DoorOutside, _speed, PoloColor, HeadColor, "cousin", LabelColor, out NavMeshAgent agent, out _npc, out _label);
            PrimitiveFactory.Visual("Visor", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 2.05f, 0.15f), new Vector3(0.5f, 0.01f, 0.5f), MaterialLibrary.Get(VisorColor));
            _stack = PrimitiveFactory.Visual("Plate Stack", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, HandHeight, HandForward), new Vector3(0.3f, PlateThickness * 0.5f, 0.3f), MaterialLibrary.Get(PlateColor));
            _stack.SetActive(false);

            _tables = FindObjectsByType<DiningTable>(FindObjectsSortMode.None);
            _phase = Phase.Idle;
            _recheckTimer = 0f;
            GameEvents.RaiseNotice("A cousin in a visor has walked in. He has not said hello. He is looking at the tables. (audio cue: the door bell, and a man who says nothing)");
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (!_timeUp)
            {
                _stayTimer -= dt;
                if (_stayTimer <= 0f)
                {
                    _timeUp = true;
                    OnTimeUp();
                    return;
                }
            }

            switch (_phase)
            {
                case Phase.Idle:
                    _recheckTimer -= dt;
                    if (_recheckTimer <= 0f)
                    {
                        _recheckTimer = RecheckSeconds;
                        PickTable();
                    }
                    break;
                case Phase.Clearing:
                    _clearTimer -= dt;
                    if (_clearTimer <= 0f) FinishClearing();
                    break;
            }
        }

        /// <summary>The dirtiest table in the room (nearest wins a tie), or null when every table is clean.</summary>
        private DiningTable DirtiestTable()
        {
            DiningTable best = null;
            int bestPlates = 0;
            float bestDistance = float.MaxValue;
            if (_tables == null) return null;
            for (int i = 0; i < _tables.Length; i++)
            {
                DiningTable table = _tables[i];
                if (table == null || table.DirtyPlates <= 0) continue;
                float distance = ChaosActors.HorizontalDistance(table.transform.position, _figure.transform.position);
                if (table.DirtyPlates > bestPlates || (table.DirtyPlates == bestPlates && distance < bestDistance))
                {
                    best = table;
                    bestPlates = table.DirtyPlates;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void PickTable()
        {
            if (IsFinished || _phase != Phase.Idle) return;
            DiningTable table = DirtiestTable();
            if (table == null)
            {
                // Nothing to clear: drift around the floor and look again in a few seconds.
                if (!_npc.IsWandering) _npc.Configure(Ctx.FloorBounds, _speed, Ctx.Rng);
                return;
            }
            _target = table;
            _phase = Phase.ToTable;
            _npc.StopWandering();
            Vector3 standPoint = ChaosActors.SampleNavMesh(table.transform.position + new Vector3(1.1f, 0f, 0f), 2f);
            _npc.GoTo(standPoint, OnReachedTable);
        }

        private void OnReachedTable()
        {
            if (IsFinished || _phase != Phase.ToTable) return;
            if (_target == null || _target.DirtyPlates <= 0)
            {
                // Somebody (the player, probably) got there first.
                BackToIdle();
                return;
            }
            _phase = Phase.Clearing;
            _clearTimer = _clearSeconds;
        }

        private void FinishClearing()
        {
            if (IsFinished || _phase != Phase.Clearing) return;
            int plates = _target != null ? _target.ClearAllPlates() : 0;
            _target = null;
            if (plates <= 0)
            {
                BackToIdle();
                return;
            }
            _carrying = plates;
            ShowStack(plates);
            GameEvents.RaiseNotice($"The cousin cleared {plates} plates off a table without a word. (audio cue: china, stacked fast)");
            _phase = Phase.ToDishwasher;
            _npc.GoTo(Ctx.DishwasherPoint, OnReachedDishwasher);
        }

        private void OnReachedDishwasher()
        {
            if (IsFinished || _phase != Phase.ToDishwasher) return;
            DeliverStack();
            if (_timeUp) Leave();
            else BackToIdle();
        }

        /// <summary>Whatever is in his hands goes into the dishwasher, through the bus.</summary>
        private void DeliverStack()
        {
            if (_carrying <= 0) return;
            GameEvents.RaisePlatesDelivered(_carrying, Ctx.DishwasherPoint);
            _total += _carrying;
            GameEvents.RaiseNotice($"The cousin loaded {_carrying} plates into the dishwasher. {_total} so far, zero words. (audio cue: a dish rack sliding home)");
            _carrying = 0;
            _stack.SetActive(false);
        }

        private void BackToIdle()
        {
            _target = null;
            _phase = Phase.Idle;
            _recheckTimer = 0f;
        }

        private void ShowStack(int plates)
        {
            float halfHeight = Mathf.Max(1, plates) * PlateThickness * 0.5f;
            _stack.transform.localScale = new Vector3(0.3f, halfHeight, 0.3f);
            _stack.transform.localPosition = new Vector3(0f, HandHeight + halfHeight, HandForward);
            _stack.SetActive(true);
        }

        private void OnTimeUp()
        {
            switch (_phase)
            {
                case Phase.Idle:
                case Phase.ToTable:
                    // Nothing in his hands: straight out.
                    Leave();
                    break;
                case Phase.Clearing:
                case Phase.ToDishwasher:
                    // Mid-carry (or about to be): the stack reaches the dishwasher first, then he goes.
                    break;
            }
        }

        private void Leave()
        {
            if (IsFinished || _phase == Phase.Leaving) return;
            _phase = Phase.Leaving;
            _target = null;
            _npc.StopWandering();
            if (_label != null) _label.text = "cousin (leaving)";
            GameEvents.RaiseNotice(_total > 0
                ? "The cousin put down the last stack, straightened the visor and walked out. He did not wave. (audio cue: the door bell, once)"
                : "The cousin found nothing to clear, stood near the door for a while, and left. (audio cue: the door bell, once, slightly disappointed)");
            _npc.GoTo(Ctx.DoorOutside, () => Finish(true, $"Cleared {_total} plates and left without a word"));
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
