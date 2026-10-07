using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Items;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// The pitcher boy: a blue plastic pitcher comes through the wall by the tables, says something
    /// that is not "oh yeah", and leaves by the front door. Behind him: a hole in the wall (duct tape,
    /// or the landlord bills you at close), a couple of spills (mop them) and some money on the floor.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Pitcher Boy", fileName = "PitcherBoy")]
    public sealed class PitcherBoyEvent : ChaosEvent
    {
        [SerializeField] private int spillCount = 2;
        [SerializeField] private float minCash = 8f;
        [SerializeField] private float maxCash = 16f;
        [SerializeField] private float gloatSeconds = 6f;
        [SerializeField] private float speed = 3.6f;

        public int SpillCount => spillCount;
        public float MinCash => minCash;
        public float MaxCash => maxCash;
        public float GloatSeconds => gloatSeconds;
        public float Speed => speed;

        public static PitcherBoyEvent CreateDefault()
        {
            var e = CreateInstance<PitcherBoyEvent>();
            e.Configure("PitcherBoy", "Pitcher boy", "Something blue just came through the wall.", 1f, 1, 240f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<PitcherBoyRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="PitcherBoyEvent"/>: the entrance, the line, the exit, and the mess he leaves.</summary>
    public sealed class PitcherBoyRunner : ChaosEventRunner
    {
        private const float SafetySeconds = 60f;
        private static readonly Color PitcherBlue = new Color(0.15f, 0.35f, 0.9f);
        private static readonly Color JuiceColor = new Color(0.85f, 0.15f, 0.2f);

        private readonly List<SpillPuddle> _puddles = new List<SpillPuddle>();
        private PitcherBoyEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private bool _gloating;
        private float _gloatTimer;
        private float _safetyTimer = SafetySeconds;
        private bool _gone;

        protected override void OnBegin()
        {
            _event = Definition as PitcherBoyEvent;
            if (Ctx.WallHole != null)
            {
                Ctx.WallHole.Break();
                Ctx.WallHole.Repaired += OnHoleTaped;
            }

            Vector3 entry = Ctx.WallHolePoint;
            BuildFigure(entry);
            SpawnMess(entry);

            string line = ChaosActors.Pick(Ctx.Rng, "OH... NO.", "MY BAD.", "WHO PUT A WALL HERE.", "I AM SO SORRY. (he is not sorry)", "IS THIS THE DMV.", "OHHH YEA- no. No.");
            GameEvents.RaiseNotice($"CRASH. A six-foot blue pitcher came through the wall. \"{line}\" (audio cue: drywall, then a wet slosh)");
            _gloating = true;
            _gloatTimer = _event != null ? _event.GloatSeconds : 6f;
            _npc.Configure(Ctx.FloorBounds, _event != null ? _event.Speed : 3.6f, Ctx.Rng);
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (_gloating)
            {
                _gloatTimer -= dt;
                if (_gloatTimer <= 0f) Leave();
            }
            else if (!_gone)
            {
                _safetyTimer -= dt;
                if (_safetyTimer <= 0f) OnLeft();
            }

            _puddles.RemoveAll(p => p == null);
            CheckResolved();
        }

        private void BuildFigure(Vector3 at)
        {
            float speed = _event != null ? _event.Speed : 3.6f;
            _figure = ChaosActors.SpawnAgentRoot("Pitcher Boy", transform, at, 0.5f, 2.2f, speed, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.3f;

            Material blue = MaterialLibrary.Get(PitcherBlue);
            PrimitiveFactory.Visual("Pitcher", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 1.05f, 1.1f), blue);
            PrimitiveFactory.Visual("Juice", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 2.08f, 0f), new Vector3(0.95f, 0.04f, 0.95f), MaterialLibrary.Get(JuiceColor));
            PrimitiveFactory.Visual("Handle", PrimitiveType.Cube, _figure.transform, new Vector3(-0.7f, 1.2f, 0f), new Vector3(0.15f, 1.1f, 0.15f), blue);
            PrimitiveFactory.Visual("Spout", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 2.05f, 0.6f), new Vector3(0.35f, 0.12f, 0.35f), blue);
            PrimitiveFactory.Visual("Eye L", PrimitiveType.Sphere, _figure.transform, new Vector3(-0.18f, 1.55f, 0.52f), Vector3.one * 0.14f, MaterialLibrary.Get(Color.white));
            PrimitiveFactory.Visual("Eye R", PrimitiveType.Sphere, _figure.transform, new Vector3(0.18f, 1.55f, 0.52f), Vector3.one * 0.14f, MaterialLibrary.Get(Color.white));
            PrimitiveFactory.Visual("Mouth", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 1.25f, 0.53f), new Vector3(0.3f, 0.08f, 0.04f), MaterialLibrary.Get(Color.black));
            TextMesh label = PrimitiveFactory.Label("Label", _figure.transform, new Vector3(0f, 2.6f, 0f), "pitcher boy", 0.2f, Ctx.Font, new Color(0.6f, 0.75f, 1f));
            label.gameObject.AddComponent<Billboard>();

            _npc = _figure.AddComponent<WanderingNpc>();
        }

        /// <summary>Spills around the hole and some cash that fell out of... him.</summary>
        private void SpawnMess(Vector3 entry)
        {
            int count = _event != null ? Mathf.Max(1, _event.SpillCount) : 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = entry + new Vector3(1.2f + 1.3f * i + (float)Ctx.Rng.NextDouble(), 0f, ((float)Ctx.Rng.NextDouble() - 0.5f) * 3f);
                Vector3 point = ChaosActors.SampleNavMesh(candidate, 2f);
                _puddles.Add(SpillPuddle.Spawn($"Juice Spill {i + 1}", transform, Ctx, point, JuiceColor, 1.3f, null));
            }

            float cash = Mathf.Lerp(_event != null ? _event.MinCash : 8f, _event != null ? _event.MaxCash : 16f, (float)Ctx.Rng.NextDouble());
            int coins = Ctx.Config != null ? Mathf.Max(3, Ctx.Config.CoinsPerBurst / 2) : 5;
            CoinPickup.Burst(entry + Vector3.right * 1.5f, cash, coins, "whatever the pitcher boy had on him");
        }

        private void Leave()
        {
            _gloating = false;
            _npc.StopWandering();
            GameEvents.RaiseNotice("The pitcher boy is leaving. Through the door, this time.");
            _npc.GoTo(Ctx.DoorOutside, OnLeft);
        }

        private void OnLeft()
        {
            if (_gone) return;
            _gone = true;
            if (_figure != null) Destroy(_figure);
            _figure = null;
            WaitingOnPlayer = true; // only the hole and the juice are left; the scheduler can roll other events meanwhile
            CheckResolved();
        }

        private void OnHoleTaped()
        {
            GameEvents.RaiseNotice("The wall is held together with duct tape, which is the strongest material known to this strip mall.");
            CheckResolved();
        }

        private void CheckResolved()
        {
            if (IsFinished) return;
            bool holeFixed = Ctx.WallHole == null || !Ctx.WallHole.IsBroken;
            if (holeFixed && _puddles.Count == 0 && _gone) Finish(true, "Hole taped, juice mopped");
        }

        private void OnDestroy()
        {
            if (Ctx != null && Ctx.WallHole != null) Ctx.WallHole.Repaired -= OnHoleTaped;
        }

        public override void Abort()
        {
            // The hole outlives the event (the landlord deals with it at close); the puddles and the boy go with the runner.
            _puddles.Clear();
            EndSilently();
        }
    }
}
