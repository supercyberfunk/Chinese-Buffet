using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A bang from the restroom: a pipe on the toilet has let go and water is coming under the door.
    /// Fetch the wrench from the maintenance shelf and hold E at the pipe to stop it. While it runs
    /// the room thinks a little less of you every few seconds and the water creeps further into the
    /// dining room as puddles that stay until mopped. Ignore it long enough and the landlord's guy
    /// shuts the water off at the street, a customer who could not wait finds a lawyer, and the
    /// store pays for both.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Pipe Burst", fileName = "PipeBurst")]
    public sealed class PipeBurstEvent : ChaosEvent
    {
        [SerializeField] private float holdSeconds = 8f;
        [SerializeField] private float patienceSeconds = 120f;
        [SerializeField] private float puddleInterval = 20f;
        [SerializeField] private int maxPuddles = 5;
        [SerializeField] private float reputationInterval = 15f;
        [SerializeField] private float reputationPerTick = -1f;
        [SerializeField] private float reputationIfIgnored = -5f;

        public float HoldSeconds => holdSeconds;
        public float PatienceSeconds => patienceSeconds;
        public float PuddleInterval => puddleInterval;
        public int MaxPuddles => maxPuddles;
        public float ReputationInterval => reputationInterval;
        public float ReputationPerTick => reputationPerTick;
        public float ReputationIfIgnored => reputationIfIgnored;

        public static PipeBurstEvent CreateDefault()
        {
            var e = CreateInstance<PipeBurstEvent>();
            e.Configure("PipeBurst", "Pipe burst", "A pipe on the restroom toilet just let go.", 1f, 1, 300f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<PipeBurstRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="PipeBurstEvent"/>: the bang, the water on the floor, the wrench or the lawyer.</summary>
    public sealed class PipeBurstRunner : ChaosEventRunner
    {
        private const float PuddleStep = 1.5f;
        private const float FirstPuddleDiameter = 1.0f;
        private const float PuddleGrowth = 0.2f;
        private const float SprayHeight = 0.75f;
        private static readonly Color PipeColor = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color SprayColor = new Color(0.45f, 0.65f, 0.95f);
        private static readonly Color WaterColor = new Color(0.72f, 0.85f, 0.95f);
        private static readonly string[] SpreadLines =
        {
            "The water has found the hallway. It is not in a hurry.",
            "Somebody put a wet-floor sign in the restroom doorway. It is floating.",
            "The water is under the first table now. A customer lifted their feet and kept eating.",
            "A customer asked if the restroom was 'still open'. Nobody answered.",
        };

        private PipeBurstEvent _event;
        private Transform _spray;
        private Vector3 _sprayRest;
        private RepairPoint _repair;
        private Vector3 _flowDirection;
        private float _flowLength;
        private float _patience;
        private float _puddleTimer;
        private float _reputationTimer;
        private float _sprayPhase;
        private int _puddles;
        private bool _fixed;

        private float PuddleInterval => _event != null ? Mathf.Max(5f, _event.PuddleInterval) : 20f;
        private float ReputationInterval => _event != null ? Mathf.Max(5f, _event.ReputationInterval) : 15f;
        private int MaxPuddles => _event != null ? Mathf.Max(1, _event.MaxPuddles) : 5;

        protected override void OnBegin()
        {
            _event = Definition as PipeBurstEvent;
            Vector3 at = Ctx.RestroomPoint;

            // Where the water goes: along the floor from the restroom door toward the middle of the dining room.
            Vector3 toCenter = Ctx.FloorBounds.center - at;
            toCenter.y = 0f;
            _flowLength = toCenter.magnitude;
            _flowDirection = _flowLength > 0.01f ? toCenter / _flowLength : Vector3.forward;

            // The pipe: a short dark stub coming up out of the floor, leaning like it gave up, with the water on top.
            GameObject pipe = PrimitiveFactory.Visual("Broken Pipe", PrimitiveType.Cylinder, transform, at + new Vector3(0f, 0.3f, 0f), new Vector3(0.12f, 0.3f, 0.12f), MaterialLibrary.Get(PipeColor));
            pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            PrimitiveFactory.Visual("Fitting", PrimitiveType.Cylinder, pipe.transform, new Vector3(0f, 0.9f, 0f), new Vector3(1.5f, 0.12f, 1.5f), MaterialLibrary.Get(PipeColor));
            _sprayRest = at + new Vector3(0f, SprayHeight, 0f);
            _spray = PrimitiveFactory.Visual("Water", PrimitiveType.Sphere, transform, _sprayRest, new Vector3(0.3f, 0.4f, 0.3f), MaterialLibrary.Get(SprayColor)).transform;

            // The trigger sits on the floor, 0..1.4 m, so the pipe is lookable-at from standing height.
            _repair = RepairPoint.Create("Repair - Toilet Pipe", transform, at + Vector3.up * 0.7f, new Vector3(1.4f, 1.4f, 1.4f),
                CarryItems.Wrench, "the wrench", _event != null ? _event.HoldSeconds : 8f, "Fix the toilet pipe", "the maintenance shelf in the kitchen", OnFixed);

            _patience = _event != null ? _event.PatienceSeconds : 120f;
            _puddleTimer = PuddleInterval;
            _reputationTimer = ReputationInterval;

            GameEvents.RaiseNotice("Something in the restroom just let go. There is water coming under the door. The wrench is on the maintenance shelf. (audio cue: a loud bang, then running water)");
            SpawnPuddle();
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;
            AnimateSpray(dt);
            if (_fixed) return;

            _reputationTimer -= dt;
            if (_reputationTimer <= 0f)
            {
                _reputationTimer = ReputationInterval;
                GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationPerTick : -1f, "the restroom flood");
            }

            if (_puddles < MaxPuddles)
            {
                _puddleTimer -= dt;
                if (_puddleTimer <= 0f)
                {
                    _puddleTimer = PuddleInterval;
                    SpawnPuddle();
                    if (_puddles > 1) GameEvents.RaiseNotice(ChaosActors.Pick(Ctx.Rng, SpreadLines));
                }
            }

            _patience -= dt;
            if (_patience <= 0f) GiveUp();
        }

        /// <summary>The water: a sphere that wobbles and jumps on top of the pipe until the wrench says otherwise.</summary>
        private void AnimateSpray(float dt)
        {
            if (_spray == null || !_spray.gameObject.activeSelf) return;
            _sprayPhase += dt * 14f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_sprayPhase);
            _spray.localScale = new Vector3(0.26f + 0.1f * pulse, 0.36f + 0.16f * pulse, 0.26f + 0.1f * pulse);
            _spray.position = _sprayRest + new Vector3(0.04f * Mathf.Sin(_sprayPhase * 0.7f), 0.08f * pulse, 0.04f * Mathf.Cos(_sprayPhase * 0.9f));
        }

        /// <summary>
        /// One more puddle, further along the line from the restroom door to the middle of the floor and a
        /// little bigger than the last. It belongs to the runner's parent, not the runner: fixing the pipe
        /// does not dry the floor.
        /// </summary>
        private void SpawnPuddle()
        {
            int n = _puddles;
            float along = Mathf.Min(PuddleStep * n, _flowLength);
            Vector3 point = ChaosActors.SampleNavMesh(Ctx.RestroomPoint + _flowDirection * along, 2f);
            float diameter = FirstPuddleDiameter + PuddleGrowth * n;
            SpillPuddle.Spawn($"Water {n + 1}", transform.parent, Ctx, point, WaterColor, diameter, null);
            _puddles++;
        }

        private void OnFixed()
        {
            if (IsFinished || _fixed) return;
            _fixed = true;
            if (_spray != null) _spray.gameObject.SetActive(false);
            GameEvents.RaiseNotice($"The pipe is tight again. The water stops, mostly. {PuddleCount()} of it {(_puddles == 1 ? "is" : "are")} still on the floor between here and the tables; the mop is where you left it. (audio cue: a wrench on copper, a last cough from the pipe, one drip)");
            Finish(true, "Pipe fixed; the puddles are yours to mop");
        }

        private void GiveUp()
        {
            if (IsFinished || _fixed) return;
            float fine = Ctx.Config != null ? Ctx.Config.LawsuitFine : 50f;
            GameEvents.RaiseExpenseCharged(fine, "Lawsuit: a customer and the restroom", Ctx.RegisterPoint + Vector3.up);
            GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationIfIgnored : -5f, "someone had to use the parking lot");
            GameEvents.RaiseNotice("The landlord's guy let himself in and shut the water off at the street. A customer who could not wait any longer has already spoken to a lawyer; the letter is written, it just hasn't been stamped yet. (audio cue: a valve squealing shut, then a very long silence)");
            if (_spray != null) _spray.gameObject.SetActive(false);
            Finish(false, $"Ignored; the water was shut off and a ${fine:0.00} lawsuit is in the mail");
        }

        private string PuddleCount()
        {
            return _puddles == 1 ? "One puddle" : $"{_puddles} puddles";
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
