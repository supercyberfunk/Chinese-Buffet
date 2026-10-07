using System.Collections;
using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A tray that sat under the heat lamp one hour too long gets up and leaves the chafing dish.
    /// The mystery meat empties the tray it came from, lurches up and down the line and scares the
    /// patience out of anyone waiting to be seated. Kill it (three hits with E, or rocks) and the
    /// salvage pays for the lost food plus a bonus; leave it and it eventually crawls out the front door.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Mystery Meat", fileName = "MysteryMeat")]
    public sealed class MysteryMeatEvent : ChaosEvent
    {
        [Tooltip("E presses or rocks it takes before it stops moving.")]
        [SerializeField] private int hitsToKill = 3;
        [Tooltip("Seconds between customers getting a good look at it.")]
        [SerializeField] private float scareInterval = 8f;
        [Tooltip("Share of a queued customer's patience that one look costs them.")]
        [SerializeField] private float patienceLossPerScare = 0.5f;
        [Tooltip("Seconds it stays on the floor before crawling out the front door.")]
        [SerializeField] private float lifetimeSeconds = 120f;
        [Tooltip("Bonus on top of the lost food's unit cost when it is killed (0.3 = 30%).")]
        [SerializeField] private float salvageBonus = 0.3f;
        [SerializeField] private float speed = 2.2f;

        public int HitsToKill => hitsToKill;
        public float ScareInterval => scareInterval;
        public float PatienceLossPerScare => patienceLossPerScare;
        public float LifetimeSeconds => lifetimeSeconds;
        public float SalvageBonus => salvageBonus;
        public float Speed => speed;

        public static MysteryMeatEvent CreateDefault()
        {
            var e = CreateInstance<MysteryMeatEvent>();
            e.Configure("MysteryMeat", "Mystery meat", "One of the trays has turned. It has teeth now.", 1f, 1, 400f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<MysteryMeatRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="MysteryMeatEvent"/>: one empty tray, one lumpy thing with eyes, until somebody deals with it.</summary>
    public sealed class MysteryMeatRunner : ChaosEventRunner
    {
        private const float FallbackUnitCost = 0.75f;
        private const float WanderWidth = 10f;
        private const float WanderHeight = 2f;
        private const float WanderDepth = 4f;
        private const float BlobHeight = 0.45f;
        private const float PulseSeconds = 0.25f;
        private const float DeathSeconds = 0.6f;
        private static readonly Color BlobBase = new Color(0.45f, 0.38f, 0.32f);
        private static readonly Color ToothColor = new Color(0.95f, 0.93f, 0.85f);
        private static readonly Color ScreamLight = new Color(0.55f, 0.85f, 0.4f);
        private static readonly Vector3 BlobScale = new Vector3(1.0f, 0.9f, 1.0f);
        private static readonly Vector3 SquashScale = new Vector3(1.25f, 0.55f, 1.25f);

        private MysteryMeatEvent _event;
        private GameObject _root;
        private GameObject _blob;
        private NavMeshAgent _agent;
        private WanderingNpc _npc;
        private EventActor _actor;
        private TextMesh _label;
        private Coroutine _pulse;
        private FoodDefinition _food;
        private int _unitsLost;
        private int _hits;
        private int _hitsToKill;
        private float _scareTimer;
        private float _lifeTimer;
        private bool _dead;
        private bool _leaving;

        protected override void OnBegin()
        {
            _event = Definition as MysteryMeatEvent;
            _hitsToKill = _event != null ? Mathf.Max(1, _event.HitsToKill) : 3;
            float scareInterval = _event != null ? Mathf.Max(1f, _event.ScareInterval) : 8f;
            _scareTimer = scareInterval;
            _lifeTimer = _event != null ? _event.LifetimeSeconds : 120f;

            IFoodSource tray = PickStockedTray();
            if (tray == null)
            {
                GameEvents.RaiseNotice("Something on the line just turned, then looked around, found every tray empty and gave up. Nothing to become.");
                Finish(true, "Nothing on the line to turn");
                return;
            }

            _food = tray.Food;
            _unitsLost = tray.Take(tray.Units);
            string foodName = _food != null ? _food.DisplayName : "food";

            BuildBody(tray.StandPosition);
            var area = new Bounds(Ctx.BuffetPoint, new Vector3(WanderWidth, WanderHeight, WanderDepth));
            _npc.Configure(area, _event != null ? _event.Speed : 2.2f, Ctx.Rng);

            GameEvents.RaiseLightingCueRequested(ScreamLight, 1.5f);
            GameEvents.RaiseNotice($"Something on the line just turned. The {foodName} tray is empty ({_unitsLost} units) and whatever was in it is now walking. It has teeth. (audio cue: a monster scream)");
        }

        private IFoodSource PickStockedTray()
        {
            IReadOnlyList<IFoodSource> sources = Ctx.GetFoodSources();
            var stocked = new List<IFoodSource>();
            for (int i = 0; i < sources.Count; i++) if (sources[i] != null && sources[i].Units > 0) stocked.Add(sources[i]);
            return stocked.Count > 0 ? stocked[Ctx.Rng.Next(stocked.Count)] : null;
        }

        private void BuildBody(Vector3 near)
        {
            float speed = _event != null ? _event.Speed : 2.2f;
            Vector3 start = ChaosActors.SampleNavMesh(near, 2f);
            _root = ChaosActors.SpawnAgentRoot("Mystery Meat", transform, start, 0.5f, 1.2f, speed, Ctx.Rng, out _agent);
            _agent.stoppingDistance = 0.3f;

            Color tint = _food != null ? Color.Lerp(BlobBase, _food.Color, 0.35f) : BlobBase;
            _blob = PrimitiveFactory.Visual("Blob", PrimitiveType.Sphere, _root.transform, new Vector3(0f, BlobHeight, 0f), BlobScale, MaterialLibrary.Get(tint));

            Material white = MaterialLibrary.Get(Color.white);
            Material black = MaterialLibrary.Get(Color.black);
            Material tooth = MaterialLibrary.Get(ToothColor);
            Transform blob = _blob.transform;
            // Children of the blob inherit its squash, so local scales are divided back out.
            for (int side = -1; side <= 1; side += 2)
            {
                PrimitiveFactory.Visual("Eye", PrimitiveType.Sphere, blob, new Vector3(side * 0.22f, 0.22f, 0.4f), Unscale(new Vector3(0.16f, 0.16f, 0.16f)), white);
                PrimitiveFactory.Visual("Pupil", PrimitiveType.Sphere, blob, new Vector3(side * 0.22f, 0.22f, 0.47f), Unscale(new Vector3(0.07f, 0.07f, 0.07f)), black);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = -0.24f + i * 0.12f;
                PrimitiveFactory.Visual("Tooth", PrimitiveType.Cube, blob, new Vector3(x, -0.12f, 0.45f), Unscale(new Vector3(0.07f, 0.1f, 0.05f)), tooth);
            }

            _label = PrimitiveFactory.Label("Label", _root.transform, new Vector3(0f, 1.35f, 0f), "mystery meat", 0.2f, Ctx.Font, new Color(0.8f, 1f, 0.6f));
            _label.gameObject.AddComponent<Billboard>();

            _npc = _root.AddComponent<WanderingNpc>();
            _actor = EventActor.Attach(_root, 0.6f, 1.2f);
            _actor.AimHeight = 0.6f;
            _actor.Priority = 2;
            _actor.Prompt = inv => $"[E] Hit the mystery meat ({_hits}/{_hitsToKill})";
            _actor.OnInteract = inv => Hit("You hit the mystery meat");
            _actor.Accepts = kind => !_dead && !_leaving && kind != ThrowableKind.Cookie;
            _actor.OnHit = kind =>
            {
                if (kind == ThrowableKind.Rock) Hit("The rock sank into the mystery meat");
                else GameEvents.RaiseNotice("Bonk. The dodgeball went in, came back out, and the mystery meat did not care. (audio cue: a wet slap)");
            };
        }

        /// <summary>Local scale for a child of the blob so it comes out at <paramref name="worldSize"/> despite the blob's own scale.</summary>
        private static Vector3 Unscale(Vector3 worldSize)
        {
            return new Vector3(worldSize.x / BlobScale.x, worldSize.y / BlobScale.y, worldSize.z / BlobScale.z);
        }

        private void Update()
        {
            if (IsFinished || _dead || _leaving) return;
            float dt = Time.deltaTime;

            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                CrawlOut();
                return;
            }

            _scareTimer -= dt;
            if (_scareTimer <= 0f)
            {
                _scareTimer = _event != null ? Mathf.Max(1f, _event.ScareInterval) : 8f;
                Scare();
            }
        }

        private void Scare()
        {
            CustomerAgent victim = ChaosActors.PickCustomer(Ctx, c =>
                !c.IsSeated && !c.IsPaid && !c.IsKnockedOut &&
                (c.CurrentState == CustomerAgent.State.WalkingToLine ||
                 c.CurrentState == CustomerAgent.State.WaitingInLine ||
                 c.CurrentState == CustomerAgent.State.Shopping));
            if (victim == null) return;

            float loss = _event != null ? _event.PatienceLossPerScare : 0.5f;
            victim.LosePatience(loss);
            Pulse();
            string foodName = _food != null ? _food.DisplayName : "something";
            GameEvents.RaiseNotice(ChaosActors.Pick(Ctx.Rng,
                $"{victim.CustomerName} looked into the chafing dish and the chafing dish looked back. (audio cue: a gurgle, then a scream that isn't the monster's)",
                $"The mystery meat lurched at {victim.CustomerName}. {victim.CustomerName} is reconsidering lunch. (audio cue: a wet lunge)",
                $"{victim.CustomerName} asked what the {foodName} was today. It answered. (audio cue: a low growl)",
                $"{victim.CustomerName} saw the mystery meat and the line got shorter in spirit. (audio cue: a chair scraping)"));
        }

        private void Hit(string how)
        {
            if (IsFinished || _dead || _leaving) return;
            _hits++;
            if (_hits >= _hitsToKill)
            {
                Die(how);
                return;
            }
            Pulse();
            GameEvents.RaiseNotice(ChaosActors.Pick(Ctx.Rng,
                $"{how}. It wobbled. ({_hits}/{_hitsToKill}) (audio cue: a slap on cold gravy)",
                $"{how}. Something inside it hissed. ({_hits}/{_hitsToKill}) (audio cue: a hiss)",
                $"{how}. It squashed flat, then it stood back up. ({_hits}/{_hitsToKill}) (audio cue: a squelch)"));
        }

        private void Pulse()
        {
            if (_blob == null || _dead) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(SquashAndRecover());
        }

        private IEnumerator SquashAndRecover()
        {
            Transform blob = _blob.transform;
            float t = 0f;
            while (t < PulseSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / PulseSeconds) * Mathf.PI);
                blob.localScale = Vector3.Lerp(BlobScale, SquashScale, k);
                yield return null;
            }
            blob.localScale = BlobScale;
            _pulse = null;
        }

        private void Die(string how)
        {
            if (IsFinished || _dead) return;
            _dead = true;
            _actor.Active = false;
            _npc.StopWandering();
            if (_agent != null) _agent.enabled = false;
            if (_pulse != null) StopCoroutine(_pulse);
            if (_label != null) _label.text = "mystery meat (dead)";

            float unitCost = Ctx.Config != null && Ctx.Config.UnitCost > 0f ? Ctx.Config.UnitCost : FallbackUnitCost;
            float bonus = _event != null ? Mathf.Max(0f, _event.SalvageBonus) : 0.3f;
            float amount = _unitsLost * unitCost * (1f + bonus);
            int bonusPercent = Mathf.RoundToInt(bonus * 100f);
            string foodName = _food != null ? _food.DisplayName : "food";
            GameEvents.RaiseMoneyRecovered(amount, "mystery meat salvage", _root.transform.position);
            GameEvents.RaiseNotice($"{how} and it stopped. The {foodName} is gone ({_unitsLost} units), but the salvage covered it plus {bonusPercent}%: ${amount:0.00}. Don't ask who buys it. (audio cue: a long deflating gurgle)");
            StartCoroutine(ShrinkAndFinish(amount));
        }

        private IEnumerator ShrinkAndFinish(float amount)
        {
            Transform blob = _blob.transform;
            Vector3 from = blob.localScale;
            float t = 0f;
            while (t < DeathSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / DeathSeconds);
                blob.localScale = Vector3.Lerp(from, new Vector3(0.6f, 0.02f, 0.6f), k);
                blob.localPosition = new Vector3(0f, Mathf.Lerp(BlobHeight, 0.02f, k), 0f);
                yield return null;
            }
            if (_root != null) Destroy(_root);
            Finish(true, $"Killed; ${amount:0.00} salvaged");
        }

        private void CrawlOut()
        {
            if (IsFinished || _leaving || _dead) return;
            _leaving = true;
            _actor.Active = false;
            if (_label != null) _label.text = "mystery meat (leaving)";
            GameEvents.RaiseNotice("The mystery meat is crawling for the front door. It is somebody else's problem now, and so is the empty tray. (audio cue: wet dragging, a door chime)");
            _npc.StopWandering();
            _npc.GoTo(Ctx.DoorOutside, () => Finish(false, "It crawled out the front door"));
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
