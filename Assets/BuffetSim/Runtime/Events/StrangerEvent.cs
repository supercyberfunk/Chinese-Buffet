using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Items;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// The fortune "A stranger will take what is yours." made flesh: a man in a windbreaker walks in,
    /// stands behind the register and quietly pockets every bill customers pay while he is there.
    /// Knock him out (E, or a rock) and the money falls out of his pockets, plus twenty dollars of
    /// his own. Leave him long enough and he strolls out with all of it. Weight zero: the fortune
    /// asks for him by id; the event roll never does.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Stranger", fileName = "Stranger")]
    public sealed class StrangerEvent : ChaosEvent
    {
        [Tooltip("How long he stands behind the register before strolling out with the takings.")]
        [SerializeField] private float stayingSeconds = 90f;
        [SerializeField] private float speed = 2.8f;
        [Tooltip("His own money, which falls out with yours when he goes down.")]
        [SerializeField] private float bonusDollars = 20f;
        [Tooltip("Satisfaction change when he walks out unbothered.")]
        [SerializeField] private float reputationIfIgnored = -3f;

        public float StayingSeconds => stayingSeconds;
        public float Speed => speed;
        public float BonusDollars => bonusDollars;
        public float ReputationIfIgnored => reputationIfIgnored;

        public static StrangerEvent CreateDefault()
        {
            var e = CreateInstance<StrangerEvent>();
            e.Configure("Stranger", "The stranger", "A man in a windbreaker is standing behind your register. He works here now, apparently.", 0f, 1, 60f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<StrangerRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="StrangerEvent"/>: door, register, pockets; then the floor or the door.</summary>
    public sealed class StrangerRunner : ChaosEventRunner
    {
        private const float WalkOutSafetySeconds = 30f;
        private const float FallbackKnockoutSeconds = 25f;
        private const int FallbackCoins = 12;
        /// <summary>Lying down, the root has to come up this much for the capsule to rest on the floor instead of in it.</summary>
        private const float ProneLift = 0.4f;
        private const string ThiefName = "the stranger";

        private static readonly Color WindbreakerColor = new Color(0.15f, 0.2f, 0.4f);
        private static readonly Color HeadColor = new Color(0.86f, 0.76f, 0.68f);
        private static readonly Color StripeColor = new Color(0.9f, 0.9f, 0.92f);
        private static readonly Color LabelColor = new Color(0.8f, 0.8f, 0.82f);

        private static readonly string[] PocketLines =
        {
            "He nodded at the customer. Thanked them. Pocketed it.",
            "He said \"have a good one\". The bill went into the windbreaker.",
            "He gave the customer a mint from somewhere. The money went the other way.",
            "He rang up nothing, smiled at nobody, and the windbreaker got heavier.",
        };

        private StrangerEvent _event;
        private GameObject _figure;
        private NavMeshAgent _agent;
        private WanderingNpc _npc;
        private TextMesh _label;
        private EventActor _actor;
        private bool _atRegister;
        private bool _listening;
        private bool _down;
        private bool _leaving;
        private bool _walkOutResolved;
        private string _walkOutOutcome = string.Empty;
        private float _stayTimer;
        private float _safetyTimer;
        private float _taken;
        private int _thefts;

        protected override void OnBegin()
        {
            _event = Definition as StrangerEvent;
            float speed = _event != null ? _event.Speed : 2.8f;
            _stayTimer = _event != null ? _event.StayingSeconds : 90f;

            _figure = EventActor.BuildPerson("Stranger", transform, Ctx, Ctx.DoorOutside, speed, WindbreakerColor, HeadColor, "stranger", LabelColor, out _agent, out _npc, out _label);
            // A hood over the head and a flat stripe across the chest: a windbreaker, the kind that comes free with a bank account.
            PrimitiveFactory.Visual("Hood", PrimitiveType.Cylinder, _figure.transform, new Vector3(0f, 2.06f, -0.03f), new Vector3(0.48f, 0.09f, 0.48f), MaterialLibrary.Get(WindbreakerColor));
            PrimitiveFactory.Visual("Windbreaker Stripe", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 1.15f, 0.28f), new Vector3(0.5f, 0.08f, 0.12f), MaterialLibrary.Get(StripeColor));

            _actor = EventActor.Attach(_figure, 0.4f, 1.8f);
            _actor.Prompt = inv => "[E] Knock out the stranger";
            _actor.OnInteract = inv => KnockOut("You put the stranger on the floor.");
            _actor.Accepts = kind => !_down && kind != ThrowableKind.Cookie;
            _actor.Priority = 2;
            _actor.AimHeight = 1.3f;
            _actor.OnHit = kind =>
            {
                if (kind == ThrowableKind.Rock) KnockOut("The rock caught the stranger behind the ear.");
                else GameEvents.RaiseNotice("Bonk. He straightened the windbreaker and carried on.");
            };

            GameEvents.RaiseNotice("A man in a windbreaker just walked in. He did not look at the menu. (audio cue: the door chime, one note short)");
            // The customers stand on the door side of the counter; staff stand on the room side, a counter's depth further in.
            Vector3 behindRegister = ChaosActors.SampleNavMesh(Ctx.RegisterPoint + new Vector3(0f, 0f, 2.4f), 2f);
            _npc.GoTo(behindRegister, OnReachedRegister);
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (_leaving)
            {
                // Behind any walk-out: a blocked door never keeps the event alive.
                _safetyTimer -= dt;
                if (_safetyTimer <= 0f) CompleteWalkOut();
                return;
            }

            if (_down || !_atRegister) return;
            _stayTimer -= dt;
            if (_stayTimer <= 0f) Leave();
        }

        private void OnReachedRegister()
        {
            if (IsFinished || _down || _leaving) return;
            _atRegister = true;
            Subscribe();

            // Face the customers across the counter, the way staff do.
            Vector3 toRegister = Ctx.RegisterPoint - _figure.transform.position;
            toRegister.y = 0f;
            if (toRegister.sqrMagnitude > 0.01f) _figure.transform.rotation = Quaternion.LookRotation(toRegister);

            if (_label != null) _label.text = "stranger\n(working here now)";
            GameEvents.RaiseNotice("The stranger is standing behind your register. He has not said a word and he is not going to. (audio cue: a windbreaker rustling, a till drawer that was already open)");
        }

        /// <summary>Only what the ledger actually put in the till can come out again: a comped bill leaves him nothing to pocket.</summary>
        private void OnCustomerCharged(string customerName, float credited, Vector3 where)
        {
            if (IsFinished || _down || _leaving || !_atRegister || credited <= 0f) return;
            Vector3 at = _figure != null ? _figure.transform.position : Ctx.RegisterPoint;
            var request = new TheftRequest(credited, ThiefName, at);
            GameEvents.RaiseTheftRequested(request);
            float got = Mathf.Max(0f, request.Taken);
            if (got <= 0f) return;
            _taken += got;
            _thefts++;
            if (_thefts % 3 == 0) GameEvents.RaiseNotice(ChaosActors.Pick(Ctx.Rng, PocketLines));
        }

        private void KnockOut(string how)
        {
            if (IsFinished || _down || _leaving) return;
            _down = true;
            _atRegister = false;
            Unsubscribe();
            _actor.Active = false;
            _npc.StopWandering();
            if (_agent != null)
            {
                if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
                _agent.enabled = false;
            }

            Transform body = _figure.transform;
            body.rotation = Quaternion.Euler(0f, body.eulerAngles.y, 90f);
            body.position += Vector3.up * ProneLift;
            if (_label != null) _label.text = "stranger\n(out cold)";

            float bonus = _event != null ? _event.BonusDollars : 20f;
            float amount = _taken + bonus;
            int coins = Ctx.Config != null ? Ctx.Config.CoinsPerBurst : FallbackCoins;
            CoinPickup.Burst(body.position + Vector3.up * 0.8f, amount, coins, "the stranger's pockets");
            GameEvents.RaiseNotice(_taken > 0f
                ? $"{how} ${amount:0.00} fell out of his pockets: your ${_taken:0.00}, and ${bonus:0.00} that was his. Consider it rent. (audio cue: a lot of loose change, one coin at a time)"
                : $"{how} ${amount:0.00} fell out of his pockets, all of it his. He had not got to yours yet. (audio cue: loose change, and a sigh)");

            float nap = Ctx.Config != null ? Ctx.Config.KnockoutSeconds : FallbackKnockoutSeconds;
            StartCoroutine(GetUpAndLeave(nap, amount));
        }

        private System.Collections.IEnumerator GetUpAndLeave(float nap, float recovered)
        {
            yield return new WaitForSeconds(nap);
            if (IsFinished || _figure == null) yield break;

            Transform body = _figure.transform;
            body.rotation = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
            body.position -= Vector3.up * ProneLift;
            if (_agent != null) _agent.enabled = true;
            if (_label != null) _label.text = "stranger\n(leaving)";
            GameEvents.RaiseNotice("The stranger got up, patted his empty pockets and left without a word. (audio cue: a windbreaker, quieter now)");
            StartWalkOut(true, $"Stranger knocked out, ${recovered:0.00} recovered");
        }

        private void Leave()
        {
            if (IsFinished || _down || _leaving) return;
            _atRegister = false;
            Unsubscribe();
            _actor.Active = false;
            if (_label != null) _label.text = "stranger\n(shift's over)";

            float nudge = _event != null ? _event.ReputationIfIgnored : -3f;
            GameEvents.RaiseReputationNudged(nudge, ThiefName);
            GameEvents.RaiseNotice(_taken > 0f
                ? $"The stranger checked his watch, zipped up the windbreaker and strolled out with ${_taken:0.00} of your money. Nobody stopped him. (audio cue: a zip, the door chime)"
                : "The stranger checked his watch, zipped up the windbreaker and strolled out. Nobody paid while he was there, so all he took was the experience. (audio cue: a zip, the door chime)");
            StartWalkOut(false, $"The stranger walked out with ${_taken:0.00}");
        }

        /// <summary>Sends him to the door; the trip's callback or the safety timer, whichever comes first, ends the event with this outcome.</summary>
        private void StartWalkOut(bool resolved, string outcome)
        {
            _leaving = true;
            _walkOutResolved = resolved;
            _walkOutOutcome = outcome;
            _safetyTimer = WalkOutSafetySeconds;
            _npc.GoTo(Ctx.DoorOutside, CompleteWalkOut);
        }

        private void CompleteWalkOut()
        {
            if (IsFinished) return;
            Unsubscribe();
            if (_figure != null)
            {
                Destroy(_figure);
                _figure = null;
            }
            Finish(_walkOutResolved, _walkOutOutcome);
        }

        private void Subscribe()
        {
            if (_listening) return;
            _listening = true;
            GameEvents.CustomerCharged += OnCustomerCharged;
        }

        private void Unsubscribe()
        {
            if (!_listening) return;
            _listening = false;
            GameEvents.CustomerCharged -= OnCustomerCharged;
        }

        public override void Abort()
        {
            Unsubscribe();
            EndSilently();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
