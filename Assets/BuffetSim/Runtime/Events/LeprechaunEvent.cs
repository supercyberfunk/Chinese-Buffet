using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Items;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A leprechaun sprints in from the sidewalk, grabs what he can from the till (the ledger decides
    /// how much), taunts the room for a while and runs off with it. Deck him before he leaves and the
    /// money sprays out as coins.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Leprechaun", fileName = "Leprechaun")]
    public sealed class LeprechaunEvent : ChaosEvent
    {
        [Tooltip("What he asks the till for; the ledger never hands over more than the balance.")]
        [SerializeField] private float requestedAmount = 40f;
        [SerializeField] private float runSpeed = 4.5f;
        [SerializeField] private float wanderSpeed = 3.4f;
        [Tooltip("How long he gloats on the floor before making for the door.")]
        [SerializeField] private float wanderSeconds = 25f;

        public float RequestedAmount => requestedAmount;
        public float RunSpeed => runSpeed;
        public float WanderSpeed => wanderSpeed;
        public float WanderSeconds => wanderSeconds;

        public static LeprechaunEvent CreateDefault()
        {
            var e = CreateInstance<LeprechaunEvent>();
            e.Configure("Leprechaun", "Leprechaun", "A leprechaun is robbing the till.", 1f, 1, 180f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<LeprechaunRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="LeprechaunEvent"/>: door, till, gloat, door; or the floor if the player gets there first.</summary>
    public sealed class LeprechaunRunner : ChaosEventRunner
    {
        private const float SafetySeconds = 150f;
        private const float BodyLingerSeconds = 2f;
        private const string ThiefName = "The leprechaun";

        private static readonly Color GreenColor = new Color(0.12f, 0.6f, 0.22f);
        private static readonly Color BeardColor = new Color(0.95f, 0.5f, 0.1f);
        private static readonly Color SkinColor = new Color(0.95f, 0.8f, 0.65f);
        private static readonly Color LabelColor = new Color(0.6f, 1f, 0.6f);

        private LeprechaunEvent _event;
        private GameObject _figure;
        private Transform _visualRoot;
        private NavMeshAgent _agent;
        private WanderingNpc _npc;
        private bool _robbed;
        private float _taken;
        private bool _wandering;
        private float _wanderTimer;
        private bool _escaping;
        private bool _decked;
        private float _safetyTimer = SafetySeconds;

        /// <summary>What he actually got out of the till (zero until he reaches it).</summary>
        public float Taken => _taken;

        protected override void OnBegin()
        {
            _event = Definition as LeprechaunEvent;
            BuildFigure();
            GameEvents.RaiseNotice("(fiddle music intensifies)");
            _npc.GoTo(Ctx.RegisterPoint, OnReachedRegister);
        }

        private void Update()
        {
            if (IsFinished || _decked) return;
            float dt = Time.deltaTime;

            // Whatever happens, the event does not outlive a stuck leprechaun.
            _safetyTimer -= dt;
            if (_safetyTimer <= 0f)
            {
                Escape();
                return;
            }

            if (_wandering)
            {
                _wanderTimer -= dt;
                if (_wanderTimer <= 0f) StartEscape();
            }
        }

        private void BuildFigure()
        {
            float runSpeed = _event != null ? _event.RunSpeed : 4.5f;
            _figure = ChaosActors.SpawnAgentRoot("Leprechaun", transform, Ctx.DoorOutside, 0.25f, 1.1f, runSpeed, Ctx.Rng, out _agent);
            _agent.acceleration = 40f;
            _agent.angularSpeed = 900f;
            _agent.stoppingDistance = 0.3f;

            // Everything visual hangs off one child so falling over is a single rotation.
            _visualRoot = new GameObject("Figure").transform;
            _visualRoot.SetParent(_figure.transform, false);
            Material green = MaterialLibrary.Get(GreenColor);
            PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, _visualRoot, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), green);
            PrimitiveFactory.Visual("Face", PrimitiveType.Sphere, _visualRoot, new Vector3(0f, 0.8f, 0.1f), Vector3.one * 0.3f, MaterialLibrary.Get(SkinColor));
            PrimitiveFactory.Visual("Beard", PrimitiveType.Cube, _visualRoot, new Vector3(0f, 0.62f, 0.2f), new Vector3(0.3f, 0.22f, 0.14f), MaterialLibrary.Get(BeardColor));
            PrimitiveFactory.Visual("Hat", PrimitiveType.Cylinder, _visualRoot, new Vector3(0f, 1.1f, 0f), new Vector3(0.28f, 0.12f, 0.28f), green);
            PrimitiveFactory.Visual("Brim", PrimitiveType.Cylinder, _visualRoot, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.015f, 0.42f), green);

            TextMesh label = PrimitiveFactory.Label("Label", _figure.transform, new Vector3(0f, 1.55f, 0f), "Leprechaun", 0.18f, Ctx.Font, LabelColor);
            label.gameObject.AddComponent<Billboard>();

            // Something the player's interaction ray can hit (the visuals have no colliders).
            CapsuleCollider trigger = _figure.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.height = 1.3f;
            trigger.radius = 0.35f;
            trigger.center = new Vector3(0f, 0.6f, 0f);

            _npc = _figure.AddComponent<WanderingNpc>();
            Leprechaun actor = _figure.AddComponent<Leprechaun>();
            actor.Initialize(this);
        }

        /// <summary>At the till: ask the ledger for the money (it answers synchronously), then gloat around the floor.</summary>
        private void OnReachedRegister()
        {
            if (IsFinished || _decked || _robbed) return;
            _robbed = true;

            float requested = _event != null ? _event.RequestedAmount : 40f;
            Vector3 at = _figure != null ? _figure.transform.position : Ctx.RegisterPoint;
            var request = new TheftRequest(requested, ThiefName, at);
            GameEvents.RaiseTheftRequested(request);
            _taken = Mathf.Max(0f, request.Taken);

            _wandering = true;
            _wanderTimer = _event != null ? _event.WanderSeconds : 25f;
            float wanderSpeed = _event != null ? _event.WanderSpeed : 3.4f;
            _npc.Configure(Ctx.FloorBounds, wanderSpeed, Ctx.Rng);
        }

        private void StartEscape()
        {
            if (IsFinished || _decked || _escaping) return;
            _escaping = true;
            _wandering = false;
            _npc.StopWandering();
            if (_agent != null && _agent.enabled) _agent.speed = _event != null ? _event.RunSpeed : 4.5f;
            GameEvents.RaiseNotice("The leprechaun is making for the door!");
            _npc.GoTo(Ctx.DoorOutside, Escape);
        }

        private void Escape()
        {
            if (IsFinished || _decked) return;
            string outcome = _taken > 0f
                ? $"The leprechaun got away with ${_taken:0.00}"
                : "The leprechaun left with an empty till";
            Finish(false, outcome);
        }

        /// <summary>The player punched him: he goes down, the pot sprays out as coins, and the body lingers a moment.</summary>
        public void OnDecked()
        {
            if (IsFinished || _decked) return;
            _decked = true;
            _wandering = false;
            _npc.StopWandering();
            if (_agent != null) _agent.enabled = false;
            if (_visualRoot != null)
            {
                _visualRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _visualRoot.localPosition = new Vector3(0f, 0.25f, 0f);
            }

            Vector3 at = _figure != null ? _figure.transform.position : Ctx.RegisterPoint;
            if (_robbed && _taken > 0f)
            {
                int coins = Ctx.Config != null ? Ctx.Config.CoinsPerBurst : 8;
                CoinPickup.Burst(at, _taken, coins, "the leprechaun's pot");
                GameEvents.RaiseNotice("You decked the leprechaun. Gold everywhere.");
                Finish(true, "Pot of gold recovered");
            }
            else
            {
                GameEvents.RaiseNotice("You decked the leprechaun before he got anything.");
                Finish(true, "Till protected");
            }

            // The runner goes away in half a second; the body stays on the floor a little longer.
            if (_figure != null)
            {
                _figure.transform.SetParent(null, true);
                Destroy(_figure, BodyLingerSeconds);
                _figure = null;
            }
        }

        public override void Abort()
        {
            // The figure is a child of this runner and goes with it.
            EndSilently();
        }
    }
}
