using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Items;
using BuffetSim.Player;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A customer in cargo shorts fills a two-litre Tupperware straight from the trays: a whole tray
    /// every so often for a couple of minutes, and he pays the flat rate once on the way out. Knock
    /// him out (E, or a rock) and the Tupperware drops as a carried tray you can pour back; his bill
    /// pays itself.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Tupperware Guy", fileName = "TupperwareGuy")]
    public sealed class TupperwareGuyEvent : ChaosEvent
    {
        [SerializeField] private float drainInterval = 30f;
        [SerializeField] private float stayingSeconds = 120f;

        public float DrainInterval => drainInterval;
        public float StayingSeconds => stayingSeconds;

        public static TupperwareGuyEvent CreateDefault()
        {
            var e = CreateInstance<TupperwareGuyEvent>();
            e.Configure("TupperwareGuy", "Tupperware guy", "A man in cargo shorts has a two-litre Tupperware and a plan.", 1f, 1, 300f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<TupperwareGuyRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="TupperwareGuyEvent"/>: tray to tray with a lid, until somebody stops him.</summary>
    public sealed class TupperwareGuyRunner : ChaosEventRunner
    {
        private static readonly Color ShortsColor = new Color(0.55f, 0.5f, 0.35f);
        private static readonly Color TupperwareColor = new Color(0.6f, 0.8f, 0.9f);

        private TupperwareGuyEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private TextMesh _label;
        private EventActor _actor;
        private GameObject _tupperware;
        private FoodDefinition _lastFood;
        private int _unitsTaken;
        private float _drainTimer;
        private float _stayTimer;
        private bool _atTray;
        private bool _down;
        private bool _leaving;

        protected override void OnBegin()
        {
            _event = Definition as TupperwareGuyEvent;
            _figure = EventActor.BuildPerson("Tupperware Guy", transform, Ctx, Ctx.DoorOutside, 2.8f, ShortsColor, new Color(0.9f, 0.75f, 0.65f), "tupperware guy", new Color(1f, 0.8f, 0.5f), out NavMeshAgent agent, out _npc, out _label);
            _tupperware = PrimitiveFactory.Visual("Tupperware", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 1.0f, 0.45f), new Vector3(0.5f, 0.3f, 0.4f), MaterialLibrary.Get(TupperwareColor));
            _actor = EventActor.Attach(_figure, 0.4f, 1.9f);
            _actor.Prompt = inv => _down ? "He's out cold. The Tupperware is on the floor." : "[E] Knock out the Tupperware guy";
            _actor.OnInteract = inv => KnockOut("You put the Tupperware guy on the floor.");
            _actor.Accepts = kind => !_down && kind != ThrowableKind.Cookie;
            _actor.Priority = 2;
            _actor.OnHit = kind =>
            {
                if (kind == ThrowableKind.Rock) KnockOut("The rock caught the Tupperware guy mid-scoop.");
                else GameEvents.RaiseNotice("Bonk. He kept scooping.");
            };
            _drainTimer = 4f;
            _stayTimer = _event != null ? _event.StayingSeconds : 120f;
            GameEvents.RaiseNotice("A man in cargo shorts has walked in with a two-litre Tupperware. He is not here to sit down. (audio cue: a lid burping)");
            GoToNextTray();
        }

        private void GoToNextTray()
        {
            if (IsFinished || _down || _leaving) return;
            IReadOnlyList<IFoodSource> sources = Ctx.GetFoodSources();
            var stocked = new List<IFoodSource>();
            for (int i = 0; i < sources.Count; i++) if (sources[i] != null && sources[i].Units > 0) stocked.Add(sources[i]);
            if (stocked.Count == 0)
            {
                _atTray = false;
                _npc.GoTo(ChaosActors.SampleNavMesh(Ctx.BuffetPoint, 2f), () => _atTray = true);
                return;
            }
            IFoodSource target = stocked[Ctx.Rng.Next(stocked.Count)];
            _atTray = false;
            _npc.GoTo(target.StandPosition, () => _atTray = true);
        }

        private void Update()
        {
            if (IsFinished || _down || _leaving) return;
            float dt = Time.deltaTime;
            _stayTimer -= dt;
            if (_stayTimer <= 0f)
            {
                Leave();
                return;
            }
            if (!_atTray) return;
            _drainTimer -= dt;
            if (_drainTimer <= 0f)
            {
                _drainTimer = _event != null ? Mathf.Max(5f, _event.DrainInterval) : 30f;
                Drain();
            }
        }

        private void Drain()
        {
            IReadOnlyList<IFoodSource> sources = Ctx.GetFoodSources();
            IFoodSource nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] == null || sources[i].Units <= 0) continue;
                float d = ChaosActors.HorizontalDistance(sources[i].StandPosition, _figure.transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = sources[i];
                }
            }
            if (nearest != null && best < 2.5f)
            {
                int taken = nearest.Take(nearest.Units);
                _unitsTaken += taken;
                _lastFood = nearest.Food;
                _tupperware.transform.localScale = new Vector3(0.5f, 0.3f + Mathf.Min(0.5f, _unitsTaken * 0.01f), 0.4f);
                GameEvents.RaiseNotice($"The Tupperware guy drained the {nearest.Food.DisplayName} tray: {taken} units into the box. ({_unitsTaken} so far)");
            }
            GoToNextTray();
        }

        private void KnockOut(string how)
        {
            if (IsFinished || _down) return;
            _down = true;
            _actor.Active = false;
            _npc.StopWandering();
            var agent = _figure.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            _figure.transform.rotation = Quaternion.Euler(0f, _figure.transform.eulerAngles.y, 90f);
            _figure.transform.position += Vector3.up * 0.4f;
            if (_label != null) _label.text = "zzz";
            _tupperware.SetActive(false);

            float bill = Ctx.Config != null ? Ctx.Config.BaseCustomerBill : 35f;
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = "The Tupperware guy",
                BaseAmount = bill, Deductions = 0f, UnitsWanted = _unitsTaken, UnitsTaken = _unitsTaken, Total = bill,
                WorldPosition = _figure.transform.position,
            });
            if (_lastFood != null && _unitsTaken > 0)
            {
                int units = Mathf.Min(20, _unitsTaken);
                FoodTrayPickup.Spawn(_lastFood, units, _figure.transform.position + new Vector3(0.8f, 0f, 0f), "Tupperware");
                GameEvents.RaiseNotice($"{how} His bill paid itself (${bill:0.00}). The Tupperware is on the floor with {units} {_lastFood.DisplayName} in it; pour it back.");
            }
            else
            {
                GameEvents.RaiseNotice($"{how} His bill paid itself (${bill:0.00}). The Tupperware was still empty, which is almost sad.");
            }
            StartCoroutine(Linger());
        }

        private System.Collections.IEnumerator Linger()
        {
            yield return new WaitForSeconds(4f);
            Finish(true, $"Knocked out with {_unitsTaken} units in the box");
        }

        private void Leave()
        {
            if (IsFinished || _leaving) return;
            _leaving = true;
            _actor.Active = false;
            float bill = Ctx.Config != null ? Ctx.Config.BaseCustomerBill : 35f;
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = "The Tupperware guy",
                BaseAmount = bill, Deductions = 0f, UnitsWanted = _unitsTaken, UnitsTaken = _unitsTaken, Total = bill,
                WorldPosition = _figure.transform.position,
            });
            GameEvents.RaiseNotice($"The Tupperware guy paid ${bill:0.00} at the register for {_unitsTaken} units of food and walked out with the lid on. Technically legal.");
            _npc.GoTo(Ctx.DoorOutside, () => Finish(false, $"Walked out with {_unitsTaken} units for one flat rate"));
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
