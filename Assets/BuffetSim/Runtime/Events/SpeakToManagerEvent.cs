using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Player;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A customer plants herself in front of the register and demands the manager. Stand in front
    /// of her and hold E for twenty seconds; you cannot move. She feels heard, sits, and pays 25%
    /// over. Ignore her and she tells the line about it instead: everyone queued loses patience.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Speak To The Manager", fileName = "SpeakToManager")]
    public sealed class SpeakToManagerEvent : ChaosEvent
    {
        [SerializeField] private float holdSeconds = 20f;
        [SerializeField] private float patienceSeconds = 75f;
        [SerializeField] private float billBonus = 0.25f;
        [SerializeField] private float linePatienceLoss = 0.2f;

        public float HoldSeconds => holdSeconds;
        public float PatienceSeconds => patienceSeconds;
        public float BillBonus => billBonus;
        public float LinePatienceLoss => linePatienceLoss;

        public static SpeakToManagerEvent CreateDefault()
        {
            var e = CreateInstance<SpeakToManagerEvent>();
            e.Configure("SpeakToManager", "Speak to the manager", "Someone at the register would like to speak to the manager. You are the manager.", 1f, 1, 240f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<SpeakToManagerRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="SpeakToManagerEvent"/>: twenty seconds of nodding, or the line hears about it.</summary>
    public sealed class SpeakToManagerRunner : ChaosEventRunner
    {
        private static readonly string[] Lines =
        {
            "\"I have been coming here for eleven years.\"",
            "\"The sign says all you can eat. I can eat more than this.\"",
            "\"I know the owner. Is he here? Are you him? You don't look like him.\"",
            "\"I'm not angry. I'm disappointed, and also angry.\"",
            "\"Somebody looked at me.\"",
        };

        private SpeakToManagerEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private TextMesh _label;
        private EventActor _actor;
        private float _patience;
        private float _lineTimer = 6f;
        private bool _arrived;
        private bool _resolved;

        protected override void OnBegin()
        {
            _event = Definition as SpeakToManagerEvent;
            _figure = EventActor.BuildPerson("Manager Demander", transform, Ctx, Ctx.DoorOutside, 3f, new Color(0.75f, 0.35f, 0.55f), new Color(0.9f, 0.78f, 0.7f), "wants the manager", new Color(1f, 0.7f, 0.9f), out NavMeshAgent agent, out _npc, out _label);
            PrimitiveFactory.Visual("Haircut", PrimitiveType.Sphere, _figure.transform, new Vector3(0f, 2.08f, -0.08f), new Vector3(0.5f, 0.32f, 0.5f), MaterialLibrary.Get(new Color(0.85f, 0.75f, 0.4f)));
            PrimitiveFactory.Visual("Purse", PrimitiveType.Cube, _figure.transform, new Vector3(0.42f, 0.9f, 0f), new Vector3(0.25f, 0.3f, 0.15f), MaterialLibrary.Get(new Color(0.3f, 0.2f, 0.15f)));
            _actor = EventActor.Attach(_figure, 0.4f, 1.9f);
            _actor.Prompt = inv => _resolved ? string.Empty : _arrived ? $"[Hold E] Be the manager ({Mathf.RoundToInt(HoldSeconds)}s, you can't move)" : "She wants the manager. Let her get to the register.";
            _actor.HoldSeconds = inv => _arrived && !_resolved ? HoldSeconds : 0f;
            _actor.OnHoldComplete = inv => Resolve();
            _actor.Accepts = kind => false;
            _patience = _event != null ? _event.PatienceSeconds : 75f;
            GameEvents.RaiseNotice("A woman has walked past the line and planted herself at the register. She would like to speak to the manager.");
            Vector3 spot = ChaosActors.SampleNavMesh(Ctx.RegisterPoint + new Vector3(0f, 0f, -1.2f), 2f);
            _npc.GoTo(spot, () => { _arrived = true; GameEvents.RaiseNotice($"{Lines[Ctx.Rng.Next(Lines.Length)]} Stand in front of her and hold E for {Mathf.RoundToInt(HoldSeconds)} seconds. You will not be able to move."); });
        }

        private float HoldSeconds => _event != null ? _event.HoldSeconds : 20f;

        private void Update()
        {
            if (IsFinished || _resolved) return;
            float dt = Time.deltaTime;
            _patience -= dt;
            if (_label != null) _label.text = $"wants the manager ({Mathf.CeilToInt(_patience)}s)";
            _lineTimer -= dt;
            if (_lineTimer <= 0f && _arrived)
            {
                _lineTimer = 12f;
                GameEvents.RaiseNotice($"The manager demander: {Lines[Ctx.Rng.Next(Lines.Length)]}");
            }
            if (_patience <= 0f) TellTheLine();
        }

        private void Resolve()
        {
            if (IsFinished || _resolved) return;
            _resolved = true;
            _actor.Active = false;
            float bonus = _event != null ? _event.BillBonus : 0.25f;
            float bill = (Ctx.Config != null ? Ctx.Config.BaseCustomerBill : 35f) * (1f + bonus);
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = "The manager demander",
                BaseAmount = bill, Deductions = 0f, UnitsWanted = 0, UnitsTaken = 0, Total = bill,
                WorldPosition = _figure.transform.position,
            });
            GameEvents.RaiseReputationNudged(2f, "someone felt heard");
            GameEvents.RaiseNotice($"She feels heard. She paid ${bill:0.00} (+{bonus:P0}) without eating anything, which is the best kind of customer.");
            if (_label != null) _label.text = "heard";
            _npc.GoTo(Ctx.DoorOutside, () => Finish(true, $"Heard out, ${bill:0.00} paid"));
        }

        private void TellTheLine()
        {
            if (IsFinished || _resolved) return;
            _resolved = true;
            _actor.Active = false;
            float loss = _event != null ? _event.LinePatienceLoss : 0.2f;
            int told = 0;
            IReadOnlyList<CustomerAgent> customers = Ctx.GetCustomersInStore();
            for (int i = 0; i < customers.Count; i++)
            {
                CustomerAgent c = customers[i];
                if (c == null || c.IsSeated || c.IsPaid || c.IsKnockedOut) continue;
                if (c.CurrentState != CustomerAgent.State.WaitingInLine && c.CurrentState != CustomerAgent.State.WalkingToLine) continue;
                c.LosePatience(loss);
                told++;
            }
            GameEvents.RaiseNotice(told > 0
                ? $"Nobody came. She turned around and told the line instead. {told} people in line lost {loss:P0} of their patience."
                : "Nobody came. She turned around to tell the line, but there was no line, so she told the fountain.");
            _npc.GoTo(Ctx.DoorOutside, () => Finish(false, "The line heard about it"));
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
