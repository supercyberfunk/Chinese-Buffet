using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A hooded figure walks up to the door, chants, and lets rats loose on the dining floor. The
    /// player has a minute to toss them outside (a small bounty each); every rat still around after
    /// that moves into the walls and costs satisfaction.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Rat Summoner", fileName = "RatSummoner")]
    public sealed class RatSummonerEvent : ChaosEvent
    {
        [SerializeField] private int ratCount = 3;
        [SerializeField] private float chantSeconds = 3f;
        [Tooltip("How long the player gets once the rats are loose.")]
        [SerializeField] private float ratSeconds = 60f;
        [SerializeField] private float ratSpeed = 3.2f;
        [Tooltip("Satisfaction change per rat that gets away (negative).")]
        [SerializeField] private float reputationPerRat = -5f;

        public int RatCount => ratCount;
        public float ChantSeconds => chantSeconds;
        public float RatSeconds => ratSeconds;
        public float RatSpeed => ratSpeed;
        public float ReputationPerRat => reputationPerRat;

        public static RatSummonerEvent CreateDefault()
        {
            var e = CreateInstance<RatSummonerEvent>();
            e.Configure("RatSummoner", "Rat summoner", "A hooded figure is chanting at the door.", 1f, 1, 180f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<RatSummonerRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="RatSummonerEvent"/>: the figure, the rats and the clock.</summary>
    public sealed class RatSummonerRunner : ChaosEventRunner
    {
        private const float FigureSpeed = 2f;
        private const float FigureStuckSeconds = 20f;

        private static readonly Color RobeColor = new Color(0.1f, 0.08f, 0.14f);
        private static readonly Color HoodLabelColor = new Color(0.8f, 0.55f, 1f);
        private static readonly Color FurColor = new Color(0.45f, 0.3f, 0.18f);
        private static readonly Color TailColor = new Color(0.8f, 0.6f, 0.55f);

        private readonly List<Rat> _rats = new List<Rat>();
        private RatSummonerEvent _event;
        private GameObject _figure;
        private WanderingNpc _figureNpc;
        private bool _chanting;
        private float _chantTimer;
        private bool _ratsReleased;
        private float _ratTimer;
        private float _stuckTimer;

        public int RatsRemaining => _rats.Count;

        protected override void OnBegin()
        {
            _event = Definition as RatSummonerEvent;
            _stuckTimer = FigureStuckSeconds;
            BuildFigure();
            _figureNpc.GoTo(Ctx.DoorInside, OnFigureAtDoor);
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (!_ratsReleased)
            {
                if (_chanting)
                {
                    _chantTimer -= dt;
                    if (_chantTimer <= 0f) ReleaseRats();
                }
                else
                {
                    // The figure never made it in (blocked door, no path): the rats come anyway.
                    _stuckTimer -= dt;
                    if (_stuckTimer <= 0f) OnFigureAtDoor();
                }
                return;
            }

            _ratTimer -= dt;
            if (_ratTimer <= 0f) TimeOut();
        }

        private void BuildFigure()
        {
            _figure = ChaosActors.SpawnAgentRoot("Hooded Figure", transform, Ctx.DoorOutside, 0.35f, 1.8f, FigureSpeed, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.3f;

            Material robe = MaterialLibrary.Get(RobeColor);
            PrimitiveFactory.Visual("Robe", PrimitiveType.Capsule, _figure.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.75f, 0.9f, 0.75f), robe);
            // A cone-ish hood out of two cubes turned 45 degrees.
            GameObject hood = PrimitiveFactory.Visual("Hood", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.55f, 0.5f, 0.55f), robe);
            hood.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            GameObject tip = PrimitiveFactory.Visual("Hood Tip", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 2.32f, 0f), new Vector3(0.28f, 0.3f, 0.28f), robe);
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            TextMesh label = PrimitiveFactory.Label("Label", _figure.transform, new Vector3(0f, 2.8f, 0f), "???", 0.25f, Ctx.Font, HoodLabelColor);
            label.gameObject.AddComponent<Billboard>();

            _figureNpc = _figure.AddComponent<WanderingNpc>();
        }

        private void OnFigureAtDoor()
        {
            if (IsFinished || _chanting || _ratsReleased) return;
            _chanting = true;
            _chantTimer = _event != null ? _event.ChantSeconds : 3f;
            GameEvents.RaiseNotice("A hooded figure is chanting at the door.");
        }

        private void ReleaseRats()
        {
            _ratsReleased = true;
            _ratTimer = _event != null ? _event.RatSeconds : 60f;
            int count = _event != null ? Mathf.Max(1, _event.RatCount) : 3;
            float speed = _event != null ? _event.RatSpeed : 3.2f;
            Vector3 origin = _figure != null ? _figure.transform.position : Ctx.DoorInside;
            for (int i = 0; i < count; i++) _rats.Add(SpawnRat(origin, speed, i, count));
            GameEvents.RaiseNotice($"{count} rats scurry in from the door. Catch them!");

            // The figure's work is done: back out the way it came, then gone.
            if (_figure != null && _figureNpc != null)
            {
                GameObject figure = _figure;
                _figureNpc.GoTo(Ctx.DoorOutside, () => { if (figure != null) Destroy(figure); });
            }
        }

        private Rat SpawnRat(Vector3 origin, float speed, int index, int count)
        {
            float angle = index * (360f / count) + (float)Ctx.Rng.NextDouble() * 60f;
            Vector3 near = origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.8f;
            GameObject root = ChaosActors.SpawnAgentRoot($"Rat {index + 1}", transform, near, 0.15f, 0.4f, speed, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.1f;
            agent.angularSpeed = 900f;

            // A lying capsule for the body, a thin cube for the tail.
            GameObject body = PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.18f, 0.22f), MaterialLibrary.Get(FurColor));
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrimitiveFactory.Visual("Tail", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.08f, -0.32f), new Vector3(0.03f, 0.03f, 0.3f), MaterialLibrary.Get(TailColor));
            TextMesh label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 0.55f, 0f), "rat", 0.14f, Ctx.Font, Color.white);
            label.gameObject.AddComponent<Billboard>();

            // Something the player's interaction ray can hit (the visuals have no colliders).
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.35f;
            trigger.center = new Vector3(0f, 0.2f, 0f);

            WanderingNpc npc = root.AddComponent<WanderingNpc>();
            npc.Configure(Ctx.FloorBounds, speed, Ctx.Rng);

            Rat rat = root.AddComponent<Rat>();
            rat.Initialize(this, Ctx.DoorOutside, label);
            return rat;
        }

        /// <summary>Called by a rat when it is destroyed (tossed, timed out, or cleaned up).</summary>
        public void OnRatGone(Rat rat)
        {
            _rats.Remove(rat);
            if (IsFinished || !_ratsReleased) return;
            if (_rats.Count == 0) Finish(true, "All rats evicted");
        }

        private void TimeOut()
        {
            // Copy first: destroying a rat calls back into OnRatGone.
            Rat[] remaining = _rats.ToArray();
            _rats.Clear();
            int count = 0;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] != null) count++;
            }

            if (count == 0)
            {
                Finish(true, "All rats evicted");
                return;
            }

            GameEvents.RaiseNotice("The rats moved into the walls.");
            float perRat = _event != null ? _event.ReputationPerRat : -5f;
            GameEvents.RaiseReputationNudged(perRat * count, "rats");
            Finish(false, $"{count} rat{(count == 1 ? "" : "s")} got away");
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] != null) Destroy(remaining[i].gameObject);
            }
        }

        public override void Abort()
        {
            // The figure and the rats are children of this runner and go with it.
            _rats.Clear();
            EndSilently();
        }
    }
}
