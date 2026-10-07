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
    /// Someone's grandmother has let herself behind the buffet line to help. Every few seconds she
    /// swaps a tray and serves a customer the wrong thing with tongs that are not hers; every unit
    /// she hands out is a unit missing from the plate that was supposed to get it. Set a plate of
    /// any five cooked units down for her and she sits by the nearest booth, eats, pays the flat
    /// rate and goes. Leave her and she leaves on her own, eventually. Weight 0: the scheduler
    /// never picks her, only the fortune "A relative will visit soon." does.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Grandma", fileName = "Grandma")]
    public sealed class GrandmaEvent : ChaosEvent
    {
        [SerializeField] private float swapInterval = 10f;
        [SerializeField] private int swapMin = 2;
        [SerializeField] private int swapMax = 4;
        [SerializeField] private float patienceSeconds = 90f;
        [SerializeField] private int plateUnits = 5;
        [SerializeField] private float eatSeconds = 15f;
        [SerializeField] private float speed = 2.2f;

        public float SwapInterval => swapInterval;
        public int SwapMin => swapMin;
        public int SwapMax => swapMax;
        public float PatienceSeconds => patienceSeconds;
        public int PlateUnits => plateUnits;
        public float EatSeconds => eatSeconds;
        public float Speed => speed;

        public static GrandmaEvent CreateDefault()
        {
            var e = CreateInstance<GrandmaEvent>();
            e.Configure("Grandma", "Grandma", "Someone's grandmother is behind the buffet line, helping.", 0f, 1, 60f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<GrandmaRunner>(context, parent);
        }
    }

    /// <summary>
    /// Runner for <see cref="GrandmaEvent"/>: the tongs, the wrong plates, the booth. Everything she
    /// is made of hangs under the runner, so finishing (or aborting) takes all of it with her.
    /// </summary>
    public sealed class GrandmaRunner : ChaosEventRunner
    {
        private static readonly Color CardiganColor = new Color(0.62f, 0.45f, 0.62f);
        private static readonly Color SkinColor = new Color(0.9f, 0.78f, 0.7f);
        private static readonly Color BunColor = new Color(0.82f, 0.82f, 0.8f);
        private static readonly Color GlassesColor = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color LabelColor = new Color(0.95f, 0.85f, 0.95f);

        private GrandmaEvent _event;
        private GameObject _figure;
        private WanderingNpc _npc;
        private TextMesh _label;
        private EventActor _actor;
        private int _wrongUnits;
        private float _swapTimer;
        private float _patienceTimer;
        private bool _behindLine;
        private bool _atTray;
        private bool _fed;
        private bool _leaving;

        private float SwapInterval => _event != null ? Mathf.Max(2f, _event.SwapInterval) : 10f;
        private int SwapMin => _event != null ? Mathf.Max(1, _event.SwapMin) : 2;
        private int SwapMax => _event != null ? Mathf.Max(SwapMin, _event.SwapMax) : 4;
        private int PlateUnits => _event != null ? Mathf.Max(1, _event.PlateUnits) : 5;
        private float EatSeconds => _event != null ? Mathf.Max(1f, _event.EatSeconds) : 15f;
        private float Speed => _event != null ? Mathf.Max(0.5f, _event.Speed) : 2.2f;

        protected override void OnBegin()
        {
            _event = Definition as GrandmaEvent;
            _figure = BuildGrandma();
            _actor = EventActor.Attach(_figure, 0.4f, 1.4f);
            _actor.AimHeight = 0.9f;
            _actor.Prompt = PromptFor;
            _actor.OnInteract = SetPlateDown;
            _actor.Accepts = kind => false;
            _swapTimer = SwapInterval;
            _patienceTimer = _event != null ? Mathf.Max(10f, _event.PatienceSeconds) : 90f;
            GameEvents.RaiseNotice("Someone's grandmother has walked in and gone straight behind the buffet line. Nobody's grandmother works here. (audio cue: a cardigan, and tongs that are not yours)");
            _npc.GoTo(ChaosActors.SampleNavMesh(Ctx.BuffetPoint, 2f), ArriveBehindLine);
        }

        /// <summary>About 1.4 m of cardigan, bun and glasses on a short NavMeshAgent.</summary>
        private GameObject BuildGrandma()
        {
            GameObject root = ChaosActors.SpawnAgentRoot("Grandma", transform, Ctx.DoorOutside, 0.3f, 1.4f, Speed, Ctx.Rng, out NavMeshAgent agent);
            agent.stoppingDistance = 0.4f;
            PrimitiveFactory.Visual("Cardigan", PrimitiveType.Capsule, root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.5f, 0.6f), MaterialLibrary.Get(CardiganColor));
            PrimitiveFactory.Visual("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.18f, 0f), Vector3.one * 0.36f, MaterialLibrary.Get(SkinColor));
            PrimitiveFactory.Visual("Bun", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.4f, -0.04f), Vector3.one * 0.2f, MaterialLibrary.Get(BunColor));
            PrimitiveFactory.Visual("Left Lens", PrimitiveType.Cube, root.transform, new Vector3(-0.08f, 1.2f, 0.16f), new Vector3(0.11f, 0.08f, 0.03f), MaterialLibrary.Get(GlassesColor));
            PrimitiveFactory.Visual("Right Lens", PrimitiveType.Cube, root.transform, new Vector3(0.08f, 1.2f, 0.16f), new Vector3(0.11f, 0.08f, 0.03f), MaterialLibrary.Get(GlassesColor));
            _label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 1.75f, 0f), "someone's grandma", 0.18f, Ctx.Font, LabelColor);
            _label.gameObject.AddComponent<Billboard>();
            _npc = root.AddComponent<WanderingNpc>();
            return root;
        }

        private void ArriveBehindLine()
        {
            if (IsFinished || _fed || _leaving) return;
            _behindLine = true;
            _atTray = true;
            GameEvents.RaiseNotice($"She has found the tongs. Set a plate down for her ({PlateUnits} units of anything cooked, on a tray) and she'll sit; until then she's serving.");
        }

        private void Update()
        {
            if (IsFinished || _fed || _leaving) return;
            float dt = Time.deltaTime;
            _patienceTimer -= dt;
            if (_patienceTimer <= 0f)
            {
                LeaveUnfed();
                return;
            }
            if (!_behindLine) return;
            _swapTimer -= dt;
            if (_swapTimer <= 0f && _atTray)
            {
                _swapTimer = SwapInterval;
                SwapTray();
            }
        }

        /// <summary>Takes a few units from a random stocked tray (they went to the wrong plate) and shuffles along the line.</summary>
        private void SwapTray()
        {
            IReadOnlyList<IFoodSource> sources = Ctx.GetFoodSources();
            var stocked = new List<IFoodSource>();
            for (int i = 0; i < sources.Count; i++) if (sources[i] != null && sources[i].Units > 0) stocked.Add(sources[i]);
            if (stocked.Count == 0)
            {
                ShuffleTo(ChaosActors.SampleNavMesh(Ctx.BuffetPoint, 2f));
                return;
            }

            IFoodSource tray = stocked[Ctx.Rng.Next(stocked.Count)];
            int taken = tray.Take(Ctx.Rng.Next(SwapMin, SwapMax + 1));
            if (taken > 0)
            {
                _wrongUnits += taken;
                string food = tray.Food != null ? tray.Food.DisplayName : "something";
                string line = ChaosActors.Pick(Ctx.Rng,
                    $"Grandma put {taken} {food} on a plate that was holding out for something else. The man is too polite to say anything.",
                    $"Grandma swapped the {food} tray with the one next to it. {taken} units of it went where they weren't wanted.",
                    $"Grandma served a child {taken} {food} and told him it was good for him. It is missing from his father's plate now.",
                    $"\"A little extra,\" Grandma said, and {taken} {food} landed on a plate that had asked for the other thing.",
                    $"Grandma carried the {food} tray to the wrong end of the line and handed out {taken} units of it on the way. (audio cue: tongs, clacking twice)");
                GameEvents.RaiseNotice($"{line} ({_wrongUnits} units served wrong so far)");
            }
            IFoodSource next = stocked[Ctx.Rng.Next(stocked.Count)];
            ShuffleTo(next.StandPosition);
        }

        private void ShuffleTo(Vector3 point)
        {
            _atTray = false;
            _npc.GoTo(point, () => _atTray = true);
        }

        private string PromptFor(PlayerInventory inventory)
        {
            if (_fed) return "Grandma is eating. Let her.";
            if (inventory.IsHoldingCookedFood && inventory.HeldFoodUnits >= PlateUnits)
                return $"[E] Set a plate of {inventory.HeldFood.DisplayName} down for Grandma ({PlateUnits} units)";
            return $"Grandma wants a plate: {PlateUnits} units of anything cooked, on a tray";
        }

        private void SetPlateDown(PlayerInventory inventory)
        {
            if (IsFinished || _fed || _leaving || !inventory.IsHoldingCookedFood || inventory.HeldFoodUnits < PlateUnits) return;
            string foodName = inventory.HeldFood.DisplayName;
            inventory.RemoveFoodUnits(PlateUnits);
            _fed = true;
            _actor.Active = false;
            _npc.StopWandering();
            if (_label != null) _label.text = "someone's grandma (sitting down)";
            GameEvents.RaiseNotice($"Grandma put the tongs down, looked at the plate of {foodName} and said she wasn't hungry. She is carrying it to a booth. (audio cue: a chair, dragged very slowly)");
            _npc.GoTo(SeatNearSomeone(), () => StartCoroutine(EatAndPay(foodName)));
        }

        /// <summary>A spot beside the nearest seated customer's table, or anywhere on the floor when nobody is sitting down.</summary>
        private Vector3 SeatNearSomeone()
        {
            Vector3 best = ChaosActors.SampleNavMesh(ChaosActors.RandomPointInBounds(Ctx.FloorBounds, Ctx.Rng), 3f);
            float bestDistance = float.MaxValue;
            var customers = Ctx.GetCustomersInStore();
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

        private System.Collections.IEnumerator EatAndPay(string foodName)
        {
            if (_label != null) _label.text = "someone's grandma (eating)";
            yield return new WaitForSeconds(EatSeconds);
            if (IsFinished) yield break;
            float bill = Ctx.Config != null ? Ctx.Config.BaseCustomerBill : 35f;
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = "Someone's grandmother",
                BaseAmount = bill,
                Deductions = 0f,
                UnitsWanted = PlateUnits,
                UnitsTaken = PlateUnits,
                Total = bill,
                WorldPosition = _figure.transform.position,
            });
            GameEvents.RaiseNotice($"Grandma finished the {foodName}, paid ${bill:0.00} in exact change from a coin purse and told you that you look thin. (audio cue: a coin purse snapping shut)");
            _leaving = true;
            if (_label != null) _label.text = "someone's grandma (leaving)";
            string outcome = $"Fed and paid, {_wrongUnits} units served wrong first";
            _npc.GoTo(Ctx.DoorOutside, () => Finish(true, outcome));
            yield return new WaitForSeconds(20f);
            if (!IsFinished) Finish(true, outcome);
        }

        private void LeaveUnfed()
        {
            if (IsFinished || _fed || _leaving) return;
            _leaving = true;
            _actor.Active = false;
            _npc.StopWandering();
            if (_label != null) _label.text = "someone's grandma (leaving)";
            GameEvents.RaiseNotice($"Grandma put the tongs down, said \"well,\" and left. {_wrongUnits} units went to the wrong people, and every one of them is short on a plate somewhere. (audio cue: the door, gently)");
            string outcome = $"{_wrongUnits} units served to the wrong people";
            _npc.GoTo(Ctx.DoorOutside, () => Finish(false, outcome));
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
