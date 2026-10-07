using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Items;
using BuffetSim.UI;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// The fortune "Lucky number 8." The slot machine locks on 8-8-8, the lights go red, and $88 in
    /// quarters sprays across the dining room in eight bursts over eight seconds. The noise brings
    /// eight people in off the street, and every one of them can see a quarter from the door. Grab
    /// them first. Weight 0: the scheduler never picks this on its own; only the fortune starts it.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Lucky Number 8", fileName = "LuckyEight")]
    public sealed class LuckyEightEvent : ChaosEvent
    {
        [SerializeField] private float totalDollars = 88f;
        [SerializeField] private int bursts = 8;
        [SerializeField] private float seconds = 8f;
        [SerializeField] private int customers = 8;
        [SerializeField] private int coinsPerBurst = 8;

        public float TotalDollars => totalDollars;
        public int Bursts => bursts;
        public float Seconds => seconds;
        public int Customers => customers;
        public int CoinsPerBurst => coinsPerBurst;

        public static LuckyEightEvent CreateDefault()
        {
            var e = CreateInstance<LuckyEightEvent>();
            e.Configure("LuckyEight", "Lucky number 8", "8-8-8. Quarters everywhere. The whole block heard it.", 0f, 1, 60f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<LuckyEightRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="LuckyEightEvent"/>: red lights, eight sprays of quarters, eight walk-ins.</summary>
    public sealed class LuckyEightRunner : ChaosEventRunner
    {
        private const float LabelHeight = 2.5f;
        private const float LabelBobAmplitude = 0.18f;
        private const float LabelBobSpeed = 3f;
        private static readonly Vector3 SlotMachineOffset = new Vector3(-5f, 0f, 0f);
        private static readonly Color SirenColor = new Color(1f, 0.15f, 0.1f);
        private static readonly Color JackpotRed = new Color(1f, 0.1f, 0.1f);

        private LuckyEightEvent _event;
        private TextMesh _label;
        private Vector3 _labelAnchor;
        private float _bobPhase;

        private float TotalDollars => _event != null ? Mathf.Max(0f, _event.TotalDollars) : 88f;
        private int Bursts => _event != null ? Mathf.Max(1, _event.Bursts) : 8;
        private float Seconds => _event != null ? Mathf.Max(0.5f, _event.Seconds) : 8f;
        private int Customers => _event != null ? Mathf.Max(0, _event.Customers) : 8;
        private int CoinsPerBurst => _event != null ? Mathf.Max(1, _event.CoinsPerBurst) : 8;

        protected override void OnBegin()
        {
            _event = Definition as LuckyEightEvent;
            GameEvents.RaiseLightingCueRequested(SirenColor, Seconds);
            GameEvents.RaiseNotice("8-8-8. The reels locked. The machine is thinking about it. (audio cue: a siren, a jackpot bell, every slot machine sound at once)");
            BuildLabel();
            StartCoroutine(PayOut());
        }

        private void Update()
        {
            if (IsFinished || _label == null) return;
            _bobPhase += Time.deltaTime * LabelBobSpeed;
            _label.transform.position = _labelAnchor + Vector3.up * (LabelBobAmplitude * Mathf.Sin(_bobPhase));
        }

        private void BuildLabel()
        {
            _labelAnchor = Ctx.DoorInside + SlotMachineOffset + Vector3.up * LabelHeight;
            _label = PrimitiveFactory.Label("Jackpot", transform, Vector3.zero, "8 8 8", 0.9f, Ctx.Font, JackpotRed);
            _label.transform.position = _labelAnchor;
            _label.gameObject.AddComponent<Billboard>();
        }

        private System.Collections.IEnumerator PayOut()
        {
            int bursts = Bursts;
            float interval = Seconds / bursts;
            float perBurst = TotalDollars / bursts;
            var wait = new WaitForSeconds(interval);

            for (int i = 0; i < bursts; i++)
            {
                yield return wait;
                if (IsFinished) yield break;

                Vector3 point = ChaosActors.SampleNavMesh(ChaosActors.RandomPointInBounds(Ctx.FloorBounds, Ctx.Rng), 3f);
                CoinPickup.Burst(point + Vector3.up * 0.5f, perBurst, CoinsPerBurst, "lucky number 8", null, true);

                if (i == 0)
                    GameEvents.RaiseNotice("It is paying out. In quarters. Across the dining room, not into the tray. (audio cue: a coin hopper emptying itself onto tile)");
                else if (i == bursts - 1)
                    GameEvents.RaiseNotice($"That was the last of it: ${TotalDollars:0} in quarters, somewhere between the booths and the drains.");
            }

            if (IsFinished) yield break;
            int customers = Customers;
            if (customers > 0) GameEvents.RaiseCustomerSpawnRequested(customers);
            GameEvents.RaiseNotice($"{customers} people walked in because of the noise. One of them is already looking at the floor. (audio cue: the door chime, eight times, overlapping)");
            Finish(true, $"${TotalDollars:0} in quarters and {customers} walk-ins");
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
