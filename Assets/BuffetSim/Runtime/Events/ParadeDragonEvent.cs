using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Player;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A parade dragon comes through the front door on foot: two kids and a tarp. It torches one
    /// tray down to the steel, eats off the next one, does laps of the dining room and leaves.
    /// Pull the tarp off with E: the kids run, and the tarp sells for $20 at the register.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Parade Dragon", fileName = "ParadeDragon")]
    public sealed class ParadeDragonEvent : ChaosEvent
    {
        [SerializeField] private float speed = 3f;
        [SerializeField] private int eatMin = 3;
        [SerializeField] private int eatMax = 6;
        [SerializeField] private float wanderSeconds = 40f;
        [SerializeField] private float reputationForTorching = -3f;

        public float Speed => speed;
        public int EatMin => eatMin;
        public int EatMax => eatMax;
        public float WanderSeconds => wanderSeconds;
        public float ReputationForTorching => reputationForTorching;

        public static ParadeDragonEvent CreateDefault()
        {
            var e = CreateInstance<ParadeDragonEvent>();
            e.Configure("ParadeDragon", "Parade dragon", "A parade dragon is coming through the front door. On foot.", 1f, 1, 360f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<ParadeDragonRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="ParadeDragonEvent"/>: door, torch, snack, laps, door. Or a tarp in your hands and two kids at a sprint.</summary>
    public sealed class ParadeDragonRunner : ChaosEventRunner
    {
        private const float TorchSeconds = 1.5f;
        private const float SnackSeconds = 2f;
        private const float FlameSeconds = 3f;
        private const float KidSpeedMultiplier = 1.8f;
        private const float SmokeRiseMetres = 1f;
        private static readonly Color BodyColor = new Color(0.8f, 0.1f, 0.1f);
        private static readonly Color HeadColor = new Color(0.1f, 0.6f, 0.2f);
        private static readonly Color TongueColor = new Color(1f, 0.2f, 0.3f);
        private static readonly Color TrimColor = new Color(0.95f, 0.75f, 0.2f);
        private static readonly Color LegColor = new Color(0.25f, 0.3f, 0.55f);
        private static readonly Color FlameOrange = new Color(1f, 0.5f, 0.05f);
        private static readonly Color FlameYellow = new Color(1f, 0.85f, 0.2f);
        private static readonly Color SmokeColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color SkinColor = new Color(0.9f, 0.75f, 0.65f);
        private static readonly Color[] KidShirts = { new Color(0.2f, 0.5f, 0.9f), new Color(0.9f, 0.6f, 0.2f) };

        private ParadeDragonEvent _event;
        private GameObject _dragon;
        private WanderingNpc _npc;
        private EventActor _actor;
        private GameObject _smoke;
        private float _smokeRisen;
        private float _speed;
        private float _wanderTimer;
        private bool _wandering;
        private bool _unmasked;
        private string _torchedFood = "nothing";
        private int _kidsLeft;

        protected override void OnBegin()
        {
            _event = Definition as ParadeDragonEvent;
            _speed = _event != null ? _event.Speed : 3f;
            BuildDragon();
            GameEvents.RaiseNotice("A parade dragon has come through the front door. On foot. It has four legs and two of them are arguing. (audio cue: cymbals, a drum, two kids arguing under a tarp)");
            GoToTorchTarget();
        }

        private void BuildDragon()
        {
            _dragon = ChaosActors.SpawnAgentRoot("Parade Dragon", transform, Ctx.DoorOutside, 0.5f, 2f, _speed, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.5f;
            Transform root = _dragon.transform;

            // The tarp: a long red box with a gold spine. The head: a green cube, white eyes, a red tongue.
            PrimitiveFactory.Visual("Body", PrimitiveType.Cube, root, new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 0.8f, 2.4f), MaterialLibrary.Get(BodyColor));
            PrimitiveFactory.Visual("Trim", PrimitiveType.Cube, root, new Vector3(0f, 1.55f, 0f), new Vector3(0.25f, 0.12f, 2.3f), MaterialLibrary.Get(TrimColor));
            PrimitiveFactory.Visual("Head", PrimitiveType.Cube, root, new Vector3(0f, 1.35f, 1.5f), new Vector3(0.8f, 0.7f, 0.7f), MaterialLibrary.Get(HeadColor));
            PrimitiveFactory.Visual("Eye L", PrimitiveType.Sphere, root, new Vector3(-0.25f, 1.55f, 1.85f), Vector3.one * 0.18f, MaterialLibrary.Get(Color.white));
            PrimitiveFactory.Visual("Eye R", PrimitiveType.Sphere, root, new Vector3(0.25f, 1.55f, 1.85f), Vector3.one * 0.18f, MaterialLibrary.Get(Color.white));
            PrimitiveFactory.Visual("Tongue", PrimitiveType.Cube, root, new Vector3(0f, 1.15f, 1.95f), new Vector3(0.15f, 0.05f, 0.5f), MaterialLibrary.Get(TongueColor));
            // The kids' legs, which is all you see of them until the tarp comes off.
            for (int l = 0; l < 4; l++)
                PrimitiveFactory.Visual($"Leg {l + 1}", PrimitiveType.Capsule, root, new Vector3(l % 2 == 0 ? -0.2f : 0.2f, 0.35f, l < 2 ? 0.6f : -0.6f), new Vector3(0.18f, 0.35f, 0.18f), MaterialLibrary.Get(LegColor));
            TextMesh label = PrimitiveFactory.Label("Label", root, new Vector3(0f, 2.1f, 0f), "parade dragon", 0.2f, Ctx.Font, TrimColor);
            label.gameObject.AddComponent<Billboard>();

            _npc = _dragon.AddComponent<WanderingNpc>();
            _actor = EventActor.Attach(_dragon, 0.6f, 1.6f);
            _actor.AimHeight = 1.2f;
            _actor.Priority = 1;
            _actor.Prompt = inv => inv != null && inv.HandsFree ? "[E] Pull the tarp off the dragon" : "Pull the tarp off the dragon (free your hands)";
            _actor.OnInteract = PullTarp;
            _actor.Accepts = kind => false;
        }

        private IFoodSource PickStockedTray(IFoodSource exclude)
        {
            IReadOnlyList<IFoodSource> sources = Ctx.GetFoodSources();
            var stocked = new List<IFoodSource>();
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && sources[i] != exclude && sources[i].Units > 0) stocked.Add(sources[i]);
            }
            return stocked.Count > 0 ? stocked[Ctx.Rng.Next(stocked.Count)] : null;
        }

        private void GoToTorchTarget()
        {
            if (IsFinished || _unmasked) return;
            IFoodSource target = PickStockedTray(null);
            if (target == null)
            {
                GameEvents.RaiseNotice("The dragon looked at the empty trays, sighed through a tarp, and did a lap anyway.");
                Wander();
                return;
            }
            _npc.GoTo(target.StandPosition, () => StartCoroutine(TorchAfterPause(target)));
        }

        private System.Collections.IEnumerator TorchAfterPause(IFoodSource tray)
        {
            yield return new WaitForSeconds(TorchSeconds);
            if (IsFinished || _unmasked) yield break;

            int lost = tray.Take(tray.Units);
            string food = tray.Food != null ? tray.Food.DisplayName : "food";
            _torchedFood = food;
            SpawnFlames(tray.Position);
            SpawnSmoke(tray.Position);
            GameEvents.RaiseLightingCueRequested(FlameOrange, 1.5f);
            GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationForTorching : -3f, "a torched tray");
            GameEvents.RaiseNotice($"The dragon torched the {food} tray: {lost} units gone, down to the steel. (audio cue: a propane roar, the smoke alarm considering its options)");
            GoToSnackTarget(tray);
        }

        private void GoToSnackTarget(IFoodSource torched)
        {
            if (IsFinished || _unmasked) return;
            IFoodSource target = PickStockedTray(torched);
            if (target == null)
            {
                GameEvents.RaiseNotice("The dragon looked for a second tray to eat from. There wasn't one. It took that personally.");
                Wander();
                return;
            }
            _npc.GoTo(target.StandPosition, () => StartCoroutine(SnackAfterPause(target)));
        }

        private System.Collections.IEnumerator SnackAfterPause(IFoodSource tray)
        {
            yield return new WaitForSeconds(SnackSeconds);
            if (IsFinished || _unmasked) yield break;

            int min = _event != null ? _event.EatMin : 3;
            int max = _event != null ? _event.EatMax : 6;
            int ate = tray.Take(Ctx.Rng.Next(min, Mathf.Max(min, max) + 1));
            string food = tray.Food != null ? tray.Food.DisplayName : "food";
            GameEvents.RaiseNotice(ate > 0
                ? $"The dragon ate {ate} {food} off the next tray, from both ends of the costume. (audio cue: chewing, a drum, chewing)"
                : $"The dragon found the {food} tray empty and sulked. (audio cue: one cymbal, unconvinced)");
            Wander();
        }

        private void Wander()
        {
            if (IsFinished || _unmasked) return;
            _wandering = true;
            _wanderTimer = _event != null ? _event.WanderSeconds : 40f;
            _npc.Configure(Ctx.FloorBounds, _speed, Ctx.Rng);
            GameEvents.RaiseNotice("The dragon is doing laps of the dining room. Nobody asked for this. (audio cue: cymbals, less enthusiastic now)");
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (_smoke != null && _smokeRisen < SmokeRiseMetres)
            {
                float rise = Mathf.Min(0.05f * dt, SmokeRiseMetres - _smokeRisen);
                _smokeRisen += rise;
                _smoke.transform.position += Vector3.up * rise;
            }

            if (!_wandering || _unmasked) return;
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f) Leave();
        }

        private void Leave()
        {
            if (IsFinished || _unmasked || !_wandering) return;
            _wandering = false;
            _npc.StopWandering();
            GameEvents.RaiseNotice("The dragon left through the front door. The smoke stayed. (audio cue: a drum, fading down the sidewalk)");
            _npc.GoTo(Ctx.DoorOutside, () => Finish(false, $"Torched the {_torchedFood} tray and left"));
        }

        private void PullTarp(PlayerInventory inventory)
        {
            if (IsFinished || _unmasked || inventory == null) return;
            if (!inventory.TryTakeItem(CarryItems.Tarp, "dragon tarp", 1, true, false)) // bare noun: the trash can and spills say "the ..."
            {
                GameEvents.RaiseNotice("Your hands are full, and the tarp is not a one-handed job.");
                return;
            }

            _unmasked = true;
            _wandering = false;
            _actor.Active = false;
            _npc.StopWandering();
            Vector3 at = _dragon.transform.position;
            Vector3 side = _dragon.transform.right * 0.35f;
            Destroy(_dragon);

            // Two kids where the dragon stood, both legging it for the door.
            float kidSpeed = _speed * KidSpeedMultiplier;
            GameObject kid1 = BuildKid("Kid 1", at - side, kidSpeed, KidShirts[0], out WanderingNpc npc1);
            GameObject kid2 = BuildKid("Kid 2", at + side, kidSpeed, KidShirts[1], out WanderingNpc npc2);
            _kidsLeft = 2;
            npc1.GoTo(Ctx.DoorOutside, () => OnKidGone(kid1));
            npc2.GoTo(Ctx.DoorOutside, () => OnKidGone(kid2));

            GameEvents.RaiseNotice("You pulled the tarp off. Two kids, one tarp, no dragon. They ran. The tarp sells for $20 at the register. (audio cue: a cymbal hitting the floor, two pairs of sneakers)");
        }

        private GameObject BuildKid(string name, Vector3 at, float speed, Color shirt, out WanderingNpc npc)
        {
            GameObject root = ChaosActors.SpawnAgentRoot(name, transform, at, 0.25f, 1.2f, speed, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.4f;
            PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 0.45f, 0.4f), MaterialLibrary.Get(shirt));
            PrimitiveFactory.Visual("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.1f, 0f), Vector3.one * 0.3f, MaterialLibrary.Get(SkinColor));
            TextMesh label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 1.45f, 0f), "kid", 0.15f, Ctx.Font, Color.white);
            label.gameObject.AddComponent<Billboard>();
            npc = root.AddComponent<WanderingNpc>();
            return root;
        }

        private void OnKidGone(GameObject kid)
        {
            if (kid != null) Destroy(kid);
            _kidsLeft--;
            if (_kidsLeft <= 0) Finish(true, "Tarp pulled off; the kids ran");
        }

        /// <summary>Five fading spheres over <paramref name="at"/> (the torched pan).</summary>
        private void SpawnFlames(Vector3 at)
        {
            var flames = new List<GameObject>();
            for (int i = 0; i < 5; i++)
            {
                float jitterX = ((float)Ctx.Rng.NextDouble() - 0.5f) * 0.6f;
                float jitterZ = ((float)Ctx.Rng.NextDouble() - 0.5f) * 0.6f;
                float size = 0.35f + (float)Ctx.Rng.NextDouble() * 0.2f;
                GameObject flame = PrimitiveFactory.Visual($"Flame {i + 1}", PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * size, MaterialLibrary.Get(i % 2 == 0 ? FlameOrange : FlameYellow));
                // The pan sits at counter height already, so the flames start just above it.
                flame.transform.position = at + new Vector3(jitterX, 0.25f + i * 0.22f, jitterZ);
                flames.Add(flame);
            }
            StartCoroutine(BurnOut(flames));
        }

        private System.Collections.IEnumerator BurnOut(List<GameObject> flames)
        {
            var startScales = new Vector3[flames.Count];
            for (int i = 0; i < flames.Count; i++) startScales[i] = flames[i].transform.localScale;

            float elapsed = 0f;
            while (elapsed < FlameSeconds)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(1f - elapsed / FlameSeconds);
                for (int i = 0; i < flames.Count; i++)
                {
                    if (flames[i] != null) flames[i].transform.localScale = startScales[i] * k;
                }
                yield return null;
            }
            for (int i = 0; i < flames.Count; i++)
            {
                if (flames[i] != null) Destroy(flames[i]);
            }
        }

        private void SpawnSmoke(Vector3 at)
        {
            _smoke = PrimitiveFactory.Visual("Smoke", PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.9f, MaterialLibrary.Get(SmokeColor));
            _smoke.transform.position = at + Vector3.up * 1.5f;
            _smokeRisen = 0f;
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
