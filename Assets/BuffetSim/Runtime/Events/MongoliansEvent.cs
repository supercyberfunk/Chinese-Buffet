using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// Riders on horseback come through the front door, each grabs a handful of units off a buffet
    /// tray and rides out again. For insurance reasons the store is credited for what they took, at
    /// cost. You still have to restock. Nothing to do but watch and swear.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Mongolians", fileName = "Mongolians")]
    public sealed class MongoliansEvent : ChaosEvent
    {
        [SerializeField] private int riderCount = 3;
        [SerializeField] private int minUnitsPerRider = 4;
        [SerializeField] private int maxUnitsPerRider = 8;
        [SerializeField] private float speed = 5.5f;

        public int RiderCount => riderCount;
        public int MinUnitsPerRider => minUnitsPerRider;
        public int MaxUnitsPerRider => maxUnitsPerRider;
        public float Speed => speed;

        public static MongoliansEvent CreateDefault()
        {
            var e = CreateInstance<MongoliansEvent>();
            e.Configure("Mongolians", "God damn Mongolians", "Riders. In the dining room. Going for the trays.", 1f, 1, 240f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<MongoliansRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="MongoliansEvent"/>: a rider per tray, a raid each, then the insurance claim.</summary>
    public sealed class MongoliansRunner : ChaosEventRunner
    {
        private const float SafetySeconds = 70f;
        private const float GrabSeconds = 1.2f;
        private static readonly Color HorseColor = new Color(0.45f, 0.3f, 0.18f);
        private static readonly Color RiderColor = new Color(0.55f, 0.15f, 0.15f);
        private static readonly Color HatColor = new Color(0.3f, 0.2f, 0.12f);

        private sealed class Rider
        {
            public GameObject Root;
            public WanderingNpc Npc;
            public IFoodSource Target;
            public int Wants;
            public bool Grabbed;
            public bool Gone;
        }

        private readonly List<Rider> _riders = new List<Rider>();
        private MongoliansEvent _event;
        private int _unitsTaken;
        private float _safetyTimer = SafetySeconds;

        protected override void OnBegin()
        {
            _event = Definition as MongoliansEvent;
            IReadOnlyList<IFoodSource> sources = Ctx.GetFoodSources();
            var stocked = new List<IFoodSource>();
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && sources[i].Units > 0) stocked.Add(sources[i]);
            }

            if (stocked.Count == 0)
            {
                GameEvents.RaiseNotice("Hoofbeats outside. The riders look in, see empty trays, and ride on. Insulting, honestly.");
                Finish(true, "Nothing worth raiding");
                return;
            }

            int count = _event != null ? Mathf.Max(1, _event.RiderCount) : 3;
            GameEvents.RaiseNotice("GOD DAMN MONGOLIANS. (audio cue: hoofbeats, a war cry, the bell on the door)");
            for (int i = 0; i < count; i++)
            {
                IFoodSource target = stocked[Ctx.Rng.Next(stocked.Count)];
                int min = _event != null ? _event.MinUnitsPerRider : 4;
                int max = _event != null ? _event.MaxUnitsPerRider : 8;
                _riders.Add(BuildRider(i, target, Ctx.Rng.Next(min, Mathf.Max(min, max) + 1)));
            }
        }

        private Rider BuildRider(int index, IFoodSource target, int wants)
        {
            Vector3 start = Ctx.DoorOutside + new Vector3(((float)Ctx.Rng.NextDouble() - 0.5f) * 2f, 0f, -index * 0.8f);
            GameObject root = ChaosActors.SpawnAgentRoot($"Rider {index + 1}", transform, start, 0.5f, 2.2f, _event != null ? _event.Speed : 5.5f, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.5f;
            agent.acceleration = 30f;

            // The horse: a capsule on its side, four legs, a head. The rider: a capsule, a fur hat, a bow.
            GameObject horse = PrimitiveFactory.Visual("Horse", PrimitiveType.Capsule, root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 0.9f, 0.6f), MaterialLibrary.Get(HorseColor));
            horse.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrimitiveFactory.Visual("Head", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.45f, 1.05f), new Vector3(0.3f, 0.35f, 0.55f), MaterialLibrary.Get(HorseColor));
            for (int l = 0; l < 4; l++)
                PrimitiveFactory.Visual($"Leg {l + 1}", PrimitiveType.Cube, root.transform, new Vector3(l % 2 == 0 ? -0.22f : 0.22f, 0.4f, l < 2 ? 0.5f : -0.5f), new Vector3(0.12f, 0.8f, 0.12f), MaterialLibrary.Get(HorseColor));
            PrimitiveFactory.Visual("Rider", PrimitiveType.Capsule, root.transform, new Vector3(0f, 1.75f, -0.1f), new Vector3(0.45f, 0.5f, 0.45f), MaterialLibrary.Get(RiderColor));
            PrimitiveFactory.Visual("Hat", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.35f, -0.1f), new Vector3(0.4f, 0.12f, 0.4f), MaterialLibrary.Get(HatColor));
            PrimitiveFactory.Visual("Bow", PrimitiveType.Cube, root.transform, new Vector3(0.35f, 1.9f, -0.1f), new Vector3(0.03f, 0.9f, 0.03f), MaterialLibrary.Get(new Color(0.8f, 0.7f, 0.4f)));
            TextMesh label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 2.8f, 0f), "mongolian", 0.2f, Ctx.Font, new Color(1f, 0.7f, 0.5f));
            label.gameObject.AddComponent<Billboard>();

            var rider = new Rider { Root = root, Target = target, Wants = wants };
            rider.Npc = root.AddComponent<WanderingNpc>();
            rider.Npc.GoTo(target.StandPosition, () => OnRiderAtTray(rider));
            return rider;
        }

        private void OnRiderAtTray(Rider rider)
        {
            if (IsFinished || rider.Gone || rider.Grabbed) return;
            rider.Grabbed = true;
            StartCoroutine(GrabAndRun(rider));
        }

        private System.Collections.IEnumerator GrabAndRun(Rider rider)
        {
            yield return new WaitForSeconds(GrabSeconds);
            if (IsFinished || rider.Gone) yield break;

            int taken = rider.Target != null ? rider.Target.Take(rider.Wants) : 0;
            _unitsTaken += taken;
            string foodName = rider.Target != null && rider.Target.Food != null ? rider.Target.Food.DisplayName : "food";
            GameEvents.RaiseNotice(taken > 0
                ? $"A rider scooped {taken} {foodName} straight into a saddlebag."
                : $"A rider found the {foodName} tray empty and spat on the floor.");
            if (rider.Root != null && rider.Npc != null) rider.Npc.GoTo(Ctx.DoorOutside, () => OnRiderGone(rider));
            else OnRiderGone(rider);
        }

        private void OnRiderGone(Rider rider)
        {
            if (rider.Gone) return;
            rider.Gone = true;
            if (rider.Root != null) Destroy(rider.Root);
            rider.Root = null;
            CheckDone();
        }

        private void Update()
        {
            if (IsFinished) return;
            _safetyTimer -= Time.deltaTime;
            if (_safetyTimer <= 0f)
            {
                for (int i = 0; i < _riders.Count; i++) OnRiderGone(_riders[i]);
            }
        }

        private void CheckDone()
        {
            if (IsFinished) return;
            for (int i = 0; i < _riders.Count; i++)
            {
                if (!_riders[i].Gone) return;
            }

            float unitCost = Ctx.Config != null ? Ctx.Config.UnitCost : 0.75f;
            float credit = _unitsTaken * unitCost;
            if (credit > 0f)
            {
                GameEvents.RaiseMoneyRecovered(credit, "insurance: Mongolian raid", Ctx.RegisterPoint + Vector3.up);
                GameEvents.RaiseNotice($"Insurance credited ${credit:0.00} for the {_unitsTaken} units the Mongolians took. You still have to cook more.");
                Finish(true, $"{_unitsTaken} units raided, ${credit:0.00} from insurance");
            }
            else
            {
                Finish(true, "The riders left empty-handed");
            }
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
