using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Items;
using BuffetSim.Player;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// The robbery from the notes: a man in a ski mask walks in, grabs a cut of whatever is in the
    /// till (2-10%, the ledger decides) and runs for the door like a dine-and-dasher. Tackle him (E)
    /// or put a rock in him and the take comes back as coins, plus the 20% catch bonus. Weight zero:
    /// the <see cref="RobberyScheduler"/> asks for it by id instead of the event roll.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Robbery", fileName = "Robbery")]
    public sealed class RobberyEvent : ChaosEvent
    {
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 5.2f;
        [Tooltip("How long he fumbles at the register before running.")]
        [SerializeField] private float grabSeconds = 2.5f;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float GrabSeconds => grabSeconds;

        public static RobberyEvent CreateDefault()
        {
            var e = CreateInstance<RobberyEvent>();
            e.Configure(RobberyScheduler.EventId, "Robbery", "A man in a ski mask is going for the till.", 0f, 1, 60f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<RobberyRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="RobberyEvent"/>: door, till, door; or the floor if the player gets there first.</summary>
    public sealed class RobberyRunner : ChaosEventRunner
    {
        private const float SafetySeconds = 90f;
        private const float BodyLingerSeconds = 3f;
        private const string ThiefName = "The robber";

        private static readonly Color MaskColor = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color JacketColor = new Color(0.25f, 0.25f, 0.3f);
        private static readonly Color LabelColor = new Color(1f, 0.35f, 0.3f);

        private RobberyEvent _event;
        private GameObject _figure;
        private Transform _visualRoot;
        private NavMeshAgent _agent;
        private WanderingNpc _npc;
        private TextMesh _label;
        private bool _robbed;
        private bool _grabbing;
        private float _grabTimer;
        private float _taken;
        private bool _running;
        private bool _down;
        private float _safetyTimer = SafetySeconds;

        public float Taken => _taken;
        public bool IsRunning => _running;
        public bool IsDown => _down;

        protected override void OnBegin()
        {
            _event = Definition as RobberyEvent;
            BuildFigure();
            GameEvents.RaiseNotice("A guy in a ski mask just walked in. (audio cue: tense synth)");
            _npc.GoTo(Ctx.RegisterPoint, OnReachedRegister);
        }

        private void Update()
        {
            if (IsFinished || _down) return;
            float dt = Time.deltaTime;

            _safetyTimer -= dt;
            if (_safetyTimer <= 0f)
            {
                Escape();
                return;
            }

            if (_grabbing)
            {
                _grabTimer -= dt;
                if (_grabTimer <= 0f) StartRun();
            }
        }

        private void BuildFigure()
        {
            float walk = _event != null ? _event.WalkSpeed : 3f;
            _figure = ChaosActors.SpawnAgentRoot("Robber", transform, Ctx.DoorOutside, 0.35f, 1.8f, walk, Ctx.Rng, out _agent);
            _agent.acceleration = 40f;
            _agent.angularSpeed = 900f;
            _agent.stoppingDistance = 0.4f;

            _visualRoot = new GameObject("Figure").transform;
            _visualRoot.SetParent(_figure.transform, false);
            PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, _visualRoot, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), MaterialLibrary.Get(JacketColor));
            PrimitiveFactory.Visual("Mask", PrimitiveType.Sphere, _visualRoot, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.44f, MaterialLibrary.Get(MaskColor));
            PrimitiveFactory.Visual("Eye Hole", PrimitiveType.Cube, _visualRoot, new Vector3(0f, 1.98f, 0.2f), new Vector3(0.26f, 0.06f, 0.06f), MaterialLibrary.Get(new Color(0.9f, 0.8f, 0.7f)));
            PrimitiveFactory.Visual("Bag", PrimitiveType.Cube, _visualRoot, new Vector3(0.4f, 0.9f, 0f), new Vector3(0.3f, 0.4f, 0.25f), MaterialLibrary.Get(new Color(0.55f, 0.45f, 0.3f)));

            _label = PrimitiveFactory.Label("Label", _figure.transform, new Vector3(0f, 2.45f, 0f), "???", 0.22f, Ctx.Font, LabelColor);
            _label.gameObject.AddComponent<Billboard>();

            CapsuleCollider trigger = _figure.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.height = 1.9f;
            trigger.radius = 0.4f;
            trigger.center = new Vector3(0f, 0.95f, 0f);

            _npc = _figure.AddComponent<WanderingNpc>();
            Robber actor = _figure.AddComponent<Robber>();
            actor.Initialize(this);
        }

        private void OnReachedRegister()
        {
            if (IsFinished || _down || _robbed) return;
            _robbed = true;
            _grabbing = true;
            _grabTimer = _event != null ? _event.GrabSeconds : 2.5f;

            float min = Ctx.Config != null ? Ctx.Config.RobberyMinTake : 0.02f;
            float max = Ctx.Config != null ? Ctx.Config.RobberyMaxTake : 0.1f;
            float fraction = Mathf.Lerp(min, max, (float)Ctx.Rng.NextDouble());
            Vector3 at = _figure != null ? _figure.transform.position : Ctx.RegisterPoint;
            var request = new TheftRequest(ThiefName, fraction, at);
            GameEvents.RaiseTheftRequested(request);
            _taken = Mathf.Max(0f, request.Taken);
            if (_label != null) _label.text = "ROBBER\nemptying the till";
        }

        private void StartRun()
        {
            if (IsFinished || _down || _running) return;
            _grabbing = false;
            _running = true;
            if (_agent != null && _agent.enabled) _agent.speed = _event != null ? _event.RunSpeed : 5.2f;
            if (_label != null) _label.text = "!! ROBBER\nrunning for it";
            GameEvents.RaiseNotice(_taken > 0f ? $"The robber grabbed ${_taken:0.00} and is running for the door! Tackle him!" : "The robber found an empty till and is leaving in a huff.");
            _npc.GoTo(Ctx.DoorOutside, Escape);
        }

        private void Escape()
        {
            if (IsFinished || _down) return;
            Finish(false, _taken > 0f ? $"The robber got away with ${_taken:0.00}" : "The robber left empty-handed");
        }

        /// <summary>Tackled or hit: down he goes and the take comes back with the bonus, as coins.</summary>
        public void OnTackled(string how)
        {
            if (IsFinished || _down) return;
            _down = true;
            _grabbing = false;
            _npc.StopWandering();
            if (_agent != null) _agent.enabled = false;
            if (_visualRoot != null)
            {
                _visualRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _visualRoot.localPosition = new Vector3(0f, 0.35f, 0f);
            }
            if (_label != null) _label.text = "robber\nout cold";

            Vector3 at = _figure != null ? _figure.transform.position : Ctx.RegisterPoint;
            if (_taken > 0f)
            {
                float bonus = Ctx.Config != null ? Ctx.Config.RobberyCatchBonus : 0.2f;
                float amount = _taken * (1f + bonus);
                int coins = Ctx.Config != null ? Ctx.Config.CoinsPerBurst : 8;
                CoinPickup.Burst(at, amount, coins, "the robber's bag");
                GameEvents.RaiseNotice($"{how} The bag burst: ${amount:0.00} on the floor (your ${_taken:0.00} plus a {bonus:P0} finder's fee).");
                Finish(true, $"Robber caught, ${amount:0.00} recovered");
            }
            else
            {
                GameEvents.RaiseNotice($"{how} He hadn't got anything yet.");
                Finish(true, "Robber caught before he got anything");
            }

            if (_figure != null)
            {
                _figure.transform.SetParent(null, true);
                Destroy(_figure, BodyLingerSeconds);
                _figure = null;
            }
        }

        /// <summary>A dodgeball: a short stumble, then he carries on.</summary>
        public void OnStaggered()
        {
            if (IsFinished || _down || _agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.velocity = Vector3.zero;
            _agent.isStopped = true;
            Invoke(nameof(ResumeAfterStagger), 1.2f);
        }

        private void ResumeAfterStagger()
        {
            if (IsFinished || _down || _agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.isStopped = false;
        }

        public override void Abort()
        {
            EndSilently();
        }
    }

    /// <summary>The robber's interactable surface: tackle him with E, or hit him with something.</summary>
    public sealed class Robber : MonoBehaviour, IInteractable, IThrowTarget
    {
        private RobberyRunner _owner;

        public void Initialize(RobberyRunner owner)
        {
            _owner = owner;
        }

        private bool Active => _owner != null && !_owner.IsFinished && !_owner.IsDown;

        public string GetPrompt(PlayerInventory inventory)
        {
            if (!Active) return _owner != null && _owner.IsDown ? "The robber is out cold" : string.Empty;
            return "[E] Tackle the robber!";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (!Active) return;
            _owner.OnTackled("You tackled the robber.");
        }

        public bool AcceptsThrow(ThrowableKind kind) => Active && kind != ThrowableKind.Cookie;
        public int HomingPriority => Active ? 3 : 0;
        public Vector3 AimPoint => transform.position + Vector3.up * 1.3f;

        public void OnThrowHit(ThrowableKind kind)
        {
            if (!Active) return;
            if (kind == ThrowableKind.Rock) _owner.OnTackled("The rock caught the robber in the back of the head.");
            else if (kind == ThrowableKind.Dodgeball)
            {
                _owner.OnStaggered();
                GameEvents.RaiseNotice("Bonk. The robber stumbled.");
            }
        }
    }
}
