using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Items;
using BuffetSim.Player;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A man in a cigarette costume walks in, makes a noise, drops a pack of cigarettes and leaves.
    /// That is the whole event. The cigarettes go in your pocket: stand still five seconds to light
    /// one for a minute of +20% speed.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Cigarette Man", fileName = "CigaretteMan")]
    public sealed class CigaretteManEvent : ChaosEvent
    {
        [SerializeField] private float speed = 2.6f;
        [SerializeField] private int packSize = 20;

        public float Speed => speed;
        public int PackSize => packSize;

        public static CigaretteManEvent CreateDefault()
        {
            var e = CreateInstance<CigaretteManEvent>();
            e.Configure("CigaretteMan", "Cigarette man", "A man dressed as a cigarette is here. He has made a noise.", 1f, 1, 300f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<CigaretteManRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="CigaretteManEvent"/>: in, noise, drop, out.</summary>
    public sealed class CigaretteManRunner : ChaosEventRunner
    {
        private const float SafetySeconds = 60f;
        private static readonly Color PaperColor = new Color(0.97f, 0.97f, 0.95f);
        private static readonly Color FilterColor = new Color(0.85f, 0.6f, 0.25f);
        private static readonly Color EmberColor = new Color(1f, 0.3f, 0.05f);

        private CigaretteManEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private float _safetyTimer = SafetySeconds;
        private bool _dropped;
        private bool _gone;

        protected override void OnBegin()
        {
            _event = Definition as CigaretteManEvent;
            BuildFigure();
            Vector3 spot = ChaosActors.SampleNavMesh(ChaosActors.RandomPointInBounds(Ctx.FloorBounds, Ctx.Rng), 3f);
            GameEvents.RaiseNotice("A man dressed as a cigarette has walked in.");
            _npc.GoTo(spot, OnArrived);
        }

        private void Update()
        {
            if (IsFinished) return;
            _safetyTimer -= Time.deltaTime;
            if (_safetyTimer <= 0f)
            {
                if (!_dropped) DropPack();
                OnLeft();
            }
        }

        private void BuildFigure()
        {
            _figure = ChaosActors.SpawnAgentRoot("Cigarette Man", transform, Ctx.DoorOutside, 0.35f, 2.4f, _event != null ? _event.Speed : 2.6f, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.3f;
            PrimitiveFactory.Visual("Filter", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.35f, 0.7f), MaterialLibrary.Get(FilterColor));
            PrimitiveFactory.Visual("Paper", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 0.75f, 0.7f), MaterialLibrary.Get(PaperColor));
            PrimitiveFactory.Visual("Ember", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 2.25f, 0f), new Vector3(0.66f, 0.06f, 0.66f), MaterialLibrary.Get(EmberColor));
            PrimitiveFactory.Visual("Eye L", PrimitiveType.Sphere, _figure.transform, new Vector3(-0.14f, 1.7f, 0.33f), Vector3.one * 0.1f, MaterialLibrary.Get(Color.black));
            PrimitiveFactory.Visual("Eye R", PrimitiveType.Sphere, _figure.transform, new Vector3(0.14f, 1.7f, 0.33f), Vector3.one * 0.1f, MaterialLibrary.Get(Color.black));
            PrimitiveFactory.Visual("Sneaker L", PrimitiveType.Cube, _figure.transform, new Vector3(-0.18f, 0.08f, 0.15f), new Vector3(0.2f, 0.12f, 0.4f), MaterialLibrary.Get(Color.white));
            PrimitiveFactory.Visual("Sneaker R", PrimitiveType.Cube, _figure.transform, new Vector3(0.18f, 0.08f, 0.15f), new Vector3(0.2f, 0.12f, 0.4f), MaterialLibrary.Get(Color.white));
            TextMesh label = PrimitiveFactory.Label("Label", _figure.transform, new Vector3(0f, 2.7f, 0f), "cigarette man", 0.2f, Ctx.Font, new Color(1f, 0.8f, 0.6f));
            label.gameObject.AddComponent<Billboard>();
            _npc = _figure.AddComponent<WanderingNpc>();
        }

        private void OnArrived()
        {
            if (IsFinished || _dropped) return;
            string noise = ChaosActors.Pick(Ctx.Rng, "\"HONK.\"", "\"eyyyyyyy.\"", "(a long, wet cough)", "\"BLEH.\"", "(the sound a fax machine makes)", "\"Ssssssmoke.\"");
            GameEvents.RaiseNotice($"The cigarette man: {noise}");
            DropPack();
            if (_figure != null && _npc != null) _npc.GoTo(Ctx.DoorOutside, OnLeft);
        }

        private void DropPack()
        {
            if (_dropped) return;
            _dropped = true;
            Vector3 at = _figure != null ? _figure.transform.position + Vector3.right * 0.6f : Ctx.FloorBounds.center;
            int pack = _event != null ? Mathf.Max(1, _event.PackSize) : 20;
            PocketPickup.Spawn(PocketItems.Cigarettes, "cigarettes", pack, PlayerThrower.CigarettesMax, at, new Color(0.85f, 0.1f, 0.1f), PrimitiveType.Cube, 0.2f);
            GameEvents.RaiseNotice("He dropped a pack of cigarettes and did literally nothing else. (E to pocket them, left click to light one: stand still 5 s, +20% speed for a minute)");
        }

        private void OnLeft()
        {
            if (_gone || IsFinished) return;
            _gone = true;
            Finish(true, "A pack of cigarettes on the floor");
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
