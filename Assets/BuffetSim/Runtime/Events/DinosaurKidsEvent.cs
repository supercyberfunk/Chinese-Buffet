using System.Collections.Generic;
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
    /// A birthday party in dinosaur costumes storms the dining room: six to twelve small green
    /// things running laps around the tables while satisfaction leaks out the door. Dodgeballs roll
    /// in with them. A dodgeball (or a rock, if you must) to the costume sends one home crying; the
    /// rest get bored and leave on their own after a while. You can't hit a kid with your hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Dinosaur Kids", fileName = "DinosaurKids")]
    public sealed class DinosaurKidsEvent : ChaosEvent
    {
        [SerializeField] private int minKids = 6;
        [SerializeField] private int maxKids = 12;
        [Tooltip("How long the kids stay before they get bored and leave on their own.")]
        [SerializeField] private float stayingSeconds = 90f;
        [Tooltip("Dodgeballs dropped on the floor when the kids arrive.")]
        [SerializeField] private int dodgeballs = 4;
        [Tooltip("Satisfaction change every ten seconds while any kid is still running around (negative).")]
        [SerializeField] private float reputationTick = -1f;

        public int MinKids => minKids;
        public int MaxKids => maxKids;
        public float StayingSeconds => stayingSeconds;
        public int Dodgeballs => dodgeballs;
        public float ReputationTick => reputationTick;

        public static DinosaurKidsEvent CreateDefault()
        {
            var e = CreateInstance<DinosaurKidsEvent>();
            e.Configure("DinosaurKids", "Dinosaur kids", "Six to twelve kids in dinosaur costumes just stormed the dining room.", 1f, 1, 300f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<DinosaurKidsRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="DinosaurKidsEvent"/>: the herd, the dodgeballs, the satisfaction drip and the exodus.</summary>
    public sealed class DinosaurKidsRunner : ChaosEventRunner
    {
        private const float KidSpeed = 3.5f;
        private const float KidRadius = 0.25f;
        private const float KidHeight = 1.1f;
        private const float TickSeconds = 10f;
        /// <summary>Once the kids are heading for the door, the event ends by this many seconds no matter what the NavMesh thinks.</summary>
        private const float ExodusSafetySeconds = 25f;

        private static readonly Color CostumeColor = new Color(0.3f, 0.7f, 0.25f);
        private static readonly Color TailColor = new Color(0.2f, 0.5f, 0.18f);
        private static readonly Color SpikeColor = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color LabelColor = new Color(0.75f, 1f, 0.6f);
        private static readonly Color DodgeballColor = new Color(0.85f, 0.15f, 0.15f);

        private sealed class Kid
        {
            public GameObject Root;
            public WanderingNpc Npc;
            public TextMesh Label;
            public EventActor Actor;
            public bool Crying;
            public bool HeadingOut;
            public bool Gone;
        }

        private readonly List<Kid> _kids = new List<Kid>();
        private DinosaurKidsEvent _event;
        private float _stayTimer;
        private float _tickTimer;
        private int _sentHome;
        private bool _exodus;
        private bool _resolved;
        private string _outcome = string.Empty;
        private float _exodusSafety;

        protected override void OnBegin()
        {
            _event = Definition as DinosaurKidsEvent;
            int min = _event != null ? Mathf.Max(1, _event.MinKids) : 6;
            int max = _event != null ? Mathf.Max(min, _event.MaxKids) : 12;
            int count = Ctx.Rng.Next(min, max + 1);
            int balls = _event != null ? Mathf.Max(0, _event.Dodgeballs) : 4;
            _stayTimer = _event != null ? Mathf.Max(5f, _event.StayingSeconds) : 90f;
            _tickTimer = TickSeconds;

            for (int i = 0; i < count; i++) _kids.Add(BuildKid(i));
            for (int i = 0; i < balls; i++)
            {
                Vector3 point = ChaosActors.SampleNavMesh(ChaosActors.RandomPointInBounds(Ctx.FloorBounds, Ctx.Rng), 3f);
                PocketPickup.Spawn(PocketItems.Dodgeball, "dodgeball", 1, PlayerThrower.DodgeballMax, point, DodgeballColor, PrimitiveType.Sphere, 0.3f, false, transform);
            }

            GameEvents.RaiseNotice($"{count} kids in dinosaur costumes just stormed the dining room. Somebody's birthday; nobody's parent. " +
                $"{balls} dodgeballs rolled in from somewhere. Throw one (left click) at a kid. (audio cue: tiny roaring, a mother not looking up from her phone)");
        }

        private Kid BuildKid(int index)
        {
            // Through the door in a loose clump, not a single point: the NavMesh sampler spreads them out.
            Vector3 start = Ctx.DoorOutside + new Vector3(((float)Ctx.Rng.NextDouble() - 0.5f) * 1.6f, 0f, -index * 0.5f);
            GameObject root = ChaosActors.SpawnAgentRoot($"Dino Kid {index + 1}", transform, start, KidRadius, KidHeight, KidSpeed, Ctx.Rng, out NavMeshAgent agent);
            agent.angularSpeed = 720f;

            // A short green capsule, a tail that droops, one spike on the hood of the costume.
            PrimitiveFactory.Visual("Costume", PrimitiveType.Capsule, root.transform, new Vector3(0f, KidHeight * 0.5f, 0f), new Vector3(0.5f, KidHeight * 0.5f, 0.5f), MaterialLibrary.Get(CostumeColor));
            GameObject tail = PrimitiveFactory.Visual("Tail", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.35f, -0.42f), new Vector3(0.14f, 0.14f, 0.5f), MaterialLibrary.Get(TailColor));
            tail.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            GameObject spike = PrimitiveFactory.Visual("Spike", PrimitiveType.Cube, root.transform, new Vector3(0f, KidHeight + 0.08f, 0f), new Vector3(0.1f, 0.22f, 0.1f), MaterialLibrary.Get(SpikeColor));
            spike.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            TextMesh label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, KidHeight + 0.45f, 0f), "dino kid", 0.16f, Ctx.Font, LabelColor);
            label.gameObject.AddComponent<Billboard>();

            var kid = new Kid { Root = root, Label = label };
            kid.Npc = root.AddComponent<WanderingNpc>();
            kid.Npc.Configure(Ctx.FloorBounds, KidSpeed, Ctx.Rng);

            kid.Actor = EventActor.Attach(root, 0.3f, KidHeight);
            kid.Actor.AimHeight = 0.6f;
            kid.Actor.Priority = 1;
            kid.Actor.Prompt = inv => "You can't hit a kid. Throw a dodgeball (left click).";
            kid.Actor.OnInteract = inv => GameEvents.RaiseNotice("You reached for the kid. The kid bit you. Throw a dodgeball (left click); it's a legal grey area.");
            kid.Actor.Accepts = kind => !kid.Crying && (kind == ThrowableKind.Dodgeball || kind == ThrowableKind.Rock);
            kid.Actor.OnHit = kind => SendHomeCrying(kid, kind);
            return kid;
        }

        private void SendHomeCrying(Kid kid, ThrowableKind kind)
        {
            if (IsFinished || kid.Crying || kid.Gone) return;
            kid.Crying = true;
            _sentHome++;
            if (kid.Label != null) kid.Label.text = "WAAAH";
            HeadForTheDoor(kid);

            string line = kind == ThrowableKind.Rock
                ? "A rock. At a child. The kid is fine, mostly, and crying, entirely."
                : ChaosActors.Pick(Ctx.Rng,
                    "A dodgeball caught a stegosaurus square in the tail. WAAAH. It's going home.",
                    "Bonk. The T. rex is crying. The costume makes it worse somehow.",
                    "One triceratops down, weeping, heading for the door. Its mother will hear about this.",
                    "Direct hit. A small velociraptor has discovered consequences.",
                    "The dodgeball took a brontosaurus in the hood. Tears. Big ones.");
            GameEvents.RaiseNotice($"{line} ({_sentHome}/{_kids.Count}) (audio cue: one small sob, rising)");

            if (_sentHome >= _kids.Count)
            {
                GameEvents.RaiseNotice("The last dinosaur is in tears and heading for the door. Extinction event. (audio cue: several small sobs, a minivan door)");
                BeginExodus(true, $"{_sentHome} kids sent home crying");
            }
        }

        /// <summary>Stops the kid's rampage and walks it out the front; the body goes away at the door (or when the trip times out).</summary>
        private void HeadForTheDoor(Kid kid)
        {
            if (kid.HeadingOut || kid.Gone) return;
            kid.HeadingOut = true;
            if (kid.Actor != null) kid.Actor.Active = false;
            if (kid.Npc != null)
            {
                kid.Npc.StopWandering();
                kid.Npc.GoTo(Ctx.DoorOutside, () => RemoveKid(kid));
            }
            else
            {
                RemoveKid(kid);
            }
        }

        private void RemoveKid(Kid kid)
        {
            if (kid.Gone) return;
            kid.Gone = true;
            if (kid.Root != null) Destroy(kid.Root);
            kid.Root = null;
            CheckAllGone();
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (_exodus)
            {
                _exodusSafety -= dt;
                if (_exodusSafety <= 0f) Finish(_resolved, _outcome);
                return;
            }

            if (RampagingCount() == 0) return;

            _tickTimer -= dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = TickSeconds;
                float tick = _event != null ? _event.ReputationTick : -1f;
                if (!Mathf.Approximately(tick, 0f)) GameEvents.RaiseReputationNudged(tick, "dinosaur kids");
            }

            _stayTimer -= dt;
            if (_stayTimer <= 0f) LeaveOnTheirOwn();
        }

        private void LeaveOnTheirOwn()
        {
            if (IsFinished || _exodus) return;
            int bored = 0;
            for (int i = 0; i < _kids.Count; i++)
            {
                Kid kid = _kids[i];
                if (kid.Crying || kid.Gone) continue;
                bored++;
                if (kid.Label != null) kid.Label.text = "bored";
                HeadForTheDoor(kid);
            }
            GameEvents.RaiseNotice($"The dinosaur kids got bored and stampeded out the way they came. {bored} left on their own; the dodgeballs rolled out after them. (audio cue: a minivan door, a parent saying 'what do we say')");
            BeginExodus(false, $"{bored} kids left on their own");
        }

        /// <summary>Everyone still standing is already walking out; the event ends when the last body reaches the door.</summary>
        private void BeginExodus(bool resolved, string outcome)
        {
            if (IsFinished || _exodus) return;
            _exodus = true;
            _resolved = resolved;
            _outcome = outcome;
            _exodusSafety = ExodusSafetySeconds;
            CheckAllGone();
        }

        private void CheckAllGone()
        {
            if (IsFinished || !_exodus) return;
            for (int i = 0; i < _kids.Count; i++)
            {
                if (!_kids[i].Gone) return;
            }
            Finish(_resolved, _outcome);
        }

        private int RampagingCount()
        {
            int count = 0;
            for (int i = 0; i < _kids.Count; i++)
            {
                if (!_kids[i].Crying && !_kids[i].HeadingOut && !_kids[i].Gone) count++;
            }
            return count;
        }

        public override void Abort()
        {
            // The kids and the dodgeballs are children of this runner and go with it.
            _kids.Clear();
            EndSilently();
        }
    }
}
