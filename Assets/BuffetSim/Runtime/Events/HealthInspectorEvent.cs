using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Player;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A man in a blazer two sizes too small walks in, finds the nearest spill (or the buffet line if
    /// the floor is clean) and writes on a clipboard for a minute. Hand him five units of anything
    /// cooked before he finishes and he sits down as a customer, pays, and a laminated "A" goes up in
    /// the window. Let him finish and it's a "B": satisfaction takes the hit and he'll be back.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Health Inspector", fileName = "HealthInspector")]
    public sealed class HealthInspectorEvent : ChaosEvent
    {
        [SerializeField] private float writingSeconds = 60f;
        [SerializeField] private int plateUnits = 5;
        [SerializeField] private float gradeAReputation = 5f;
        [SerializeField] private float gradeBReputation = -10f;

        public float WritingSeconds => writingSeconds;
        public int PlateUnits => plateUnits;
        public float GradeAReputation => gradeAReputation;
        public float GradeBReputation => gradeBReputation;

        public static HealthInspectorEvent CreateDefault()
        {
            var e = CreateInstance<HealthInspectorEvent>();
            e.Configure("HealthInspector", "Health inspector", "A man with a clipboard is staring at your floor.", 1f, 2, 400f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<HealthInspectorRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="HealthInspectorEvent"/>: the stare, the plate, the grade.</summary>
    public sealed class HealthInspectorRunner : ChaosEventRunner
    {
        private static readonly Color BlazerColor = new Color(0.35f, 0.35f, 0.4f);
        private static readonly Color SkinColor = new Color(0.85f, 0.7f, 0.6f);

        private HealthInspectorEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private TextMesh _label;
        private EventActor _actor;
        private bool _writing;
        private bool _fed;
        private float _timer;

        protected override void OnBegin()
        {
            _event = Definition as HealthInspectorEvent;
            _figure = EventActor.BuildPerson("Health Inspector", transform, Ctx, Ctx.DoorOutside, 2.4f, BlazerColor, SkinColor, "health inspector", new Color(1f, 0.6f, 0.6f), out NavMeshAgent agent, out _npc, out _label);
            PrimitiveFactory.Visual("Clipboard", PrimitiveType.Cube, _figure.transform, new Vector3(0.35f, 1.2f, 0.25f), new Vector3(0.22f, 0.3f, 0.02f), MaterialLibrary.Get(new Color(0.9f, 0.9f, 0.85f)));
            PrimitiveFactory.Visual("Tie", PrimitiveType.Cube, _figure.transform, new Vector3(0f, 1.3f, 0.34f), new Vector3(0.08f, 0.5f, 0.02f), MaterialLibrary.Get(new Color(0.6f, 0.1f, 0.1f)));
            _actor = EventActor.Attach(_figure, 0.4f, 1.9f);
            _actor.Prompt = PromptFor;
            _actor.OnInteract = HandPlate;
            _actor.Accepts = kind => false;

            Vector3 target = NearestSpill(out bool found);
            GameEvents.RaiseNotice(found
                ? "A man in a blazer two sizes too small has walked in and is heading for a spill. He has a clipboard. (audio cue: a pen clicking)"
                : "A man in a blazer two sizes too small has walked in and is looking at the buffet line. He has a clipboard.");
            _npc.GoTo(target, StartWriting);
            _timer = (_event != null ? _event.WritingSeconds : 60f) + 15f;
        }

        private Vector3 NearestSpill(out bool found)
        {
            SpillPuddle[] puddles = FindObjectsByType<SpillPuddle>(FindObjectsSortMode.None);
            SpillPuddle best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < puddles.Length; i++)
            {
                if (puddles[i] == null || puddles[i].IsMopped) continue;
                float d = ChaosActors.HorizontalDistance(puddles[i].transform.position, Ctx.DoorInside);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = puddles[i];
                }
            }
            found = best != null;
            Vector3 point = found ? best.transform.position + new Vector3(0.9f, 0f, 0f) : Ctx.BuffetPoint;
            return ChaosActors.SampleNavMesh(point, 2f);
        }

        private void StartWriting()
        {
            if (IsFinished || _fed) return;
            _writing = true;
            _timer = _event != null ? _event.WritingSeconds : 60f;
            GameEvents.RaiseNotice($"The inspector is writing. You have about {Mathf.RoundToInt(_timer)} seconds to hand him a plate: {PlateUnits} units of anything cooked, carried on a tray.");
        }

        private int PlateUnits => _event != null ? Mathf.Max(1, _event.PlateUnits) : 5;

        private void Update()
        {
            if (IsFinished || _fed) return;
            _timer -= Time.deltaTime;
            if (_label != null && _writing) _label.text = $"inspector ({Mathf.CeilToInt(_timer)}s)";
            if (_timer <= 0f) GradeB();
        }

        private string PromptFor(PlayerInventory inventory)
        {
            if (_fed) return "The inspector is eating. Don't talk to him.";
            if (inventory.IsHoldingCookedFood)
            {
                return inventory.HeldFoodUnits >= PlateUnits
                    ? $"[E] Hand the inspector a plate of {inventory.HeldFood.DisplayName} ({PlateUnits} units)"
                    : $"The inspector wants a full plate: {PlateUnits} units, you have {inventory.HeldFoodUnits}";
            }
            return $"The inspector is writing. Bring {PlateUnits} units of anything cooked on a tray";
        }

        private void HandPlate(PlayerInventory inventory)
        {
            if (_fed || !inventory.IsHoldingCookedFood || inventory.HeldFoodUnits < PlateUnits) return;
            string foodName = inventory.HeldFood.DisplayName;
            inventory.RemoveFoodUnits(PlateUnits);
            _fed = true;
            _actor.Active = false;
            _npc.StopWandering();
            if (_label != null) _label.text = "inspector (eating)";
            GameEvents.RaiseNotice($"He takes the plate of {foodName} without looking up, sits at the nearest table and eats it with the pen still in his hand.");
            Vector3 seat = NearestSeat();
            _npc.GoTo(seat, () => StartCoroutine(EatAndGrade(foodName)));
        }

        private Vector3 NearestSeat()
        {
            Vector3 best = Ctx.BuffetPoint;
            float bestDistance = float.MaxValue;
            if (Ctx.Player == null) return best;
            var customers = Ctx.GetCustomersInStore();
            // Tables are reached through customers only; any empty-ish spot near the floor centre does for a man who sits anywhere.
            best = ChaosActors.SampleNavMesh(ChaosActors.RandomPointInBounds(Ctx.FloorBounds, Ctx.Rng), 3f);
            for (int i = 0; i < customers.Count; i++)
            {
                if (customers[i] == null || !customers[i].IsSeated) continue;
                float d = ChaosActors.HorizontalDistance(customers[i].transform.position, _figure.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = ChaosActors.SampleNavMesh(customers[i].transform.position + new Vector3(1.2f, 0f, 0f), 2f);
                }
            }
            return best;
        }

        private System.Collections.IEnumerator EatAndGrade(string foodName)
        {
            yield return new WaitForSeconds(12f);
            if (IsFinished) yield break;
            float bill = Ctx.Config != null ? Ctx.Config.BaseCustomerBill : 35f;
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = "The health inspector",
                BaseAmount = bill,
                Deductions = 0f,
                UnitsWanted = PlateUnits,
                UnitsTaken = PlateUnits,
                Total = bill,
                WorldPosition = _figure.transform.position,
            });
            GameEvents.RaiseReputationNudged(_event != null ? _event.GradeAReputation : 5f, "a laminated A in the window");
            HangGrade("A", new Color(0.3f, 0.8f, 0.4f));
            GameEvents.RaiseNotice($"The inspector paid ${bill:0.00} for the {foodName} and left a laminated A in the window. He did not look at the kitchen.");
            _npc.GoTo(Ctx.DoorOutside, () => Finish(true, "Grade A, and he paid for lunch"));
            yield return new WaitForSeconds(20f);
            if (!IsFinished) Finish(true, "Grade A, and he paid for lunch");
        }

        private void GradeB()
        {
            if (IsFinished || _fed) return;
            _fed = true;
            _actor.Active = false;
            GameEvents.RaiseReputationNudged(_event != null ? _event.GradeBReputation : -10f, "a B in the window");
            HangGrade("B", new Color(0.9f, 0.6f, 0.2f));
            GameEvents.RaiseNotice("He finished writing. A laminated B is in the window now. He said he'd be back tomorrow, like it was a kindness.");
            _npc.GoTo(Ctx.DoorOutside, () => Finish(false, "Grade B"));
        }

        /// <summary>The grade hangs inside the front window until close; the shell owns nothing, so it lives under the runner's parent.</summary>
        private void HangGrade(string grade, Color color)
        {
            Transform parent = transform.parent != null ? transform.parent : transform;
            var sign = new GameObject($"Grade {grade}");
            sign.transform.SetParent(parent, false);
            sign.transform.position = Ctx.WindowPoint + new Vector3(0.8f, 1.9f, -0.2f);
            PrimitiveFactory.Visual("Card", PrimitiveType.Cube, sign.transform, Vector3.zero, new Vector3(0.4f, 0.5f, 0.02f), MaterialLibrary.Get(Color.white));
            // The room is on the +z side of the front window: the letter sits on that face of the card,
            // turned to face it, so it reads the right way round from inside (a TextMesh reads from -z).
            TextMesh text = PrimitiveFactory.Label("Grade", sign.transform, new Vector3(0f, 0f, 0.02f), grade, 0.38f, Ctx.Font, color);
            text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            sign.AddComponent<GradeCard>();
        }

        public override void Abort()
        {
            EndSilently();
        }
    }

    /// <summary>The laminated grade in the window. Gone at close.</summary>
    public sealed class GradeCard : MonoBehaviour
    {
        private void OnEnable() => GameEvents.DayEnded += OnDayEnded;
        private void OnDisable() => GameEvents.DayEnded -= OnDayEnded;
        private void OnDayEnded(int day) => Destroy(gameObject);
    }
}
