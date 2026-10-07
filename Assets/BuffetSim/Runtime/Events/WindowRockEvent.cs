using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Items;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// Something comes through the front window and homes, egregiously, on one seated customer: a
    /// rock or, 50/50, a can of beans. Either one knocks the customer out; their bill pays itself and
    /// the table clears. The rock is yours to keep (one throw, then it is a rock again); the beans
    /// burst into a spill under the table. Fix the window with the glass pane or satisfaction keeps
    /// dropping from the draft.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Rock Through The Window", fileName = "WindowRock")]
    public sealed class WindowRockEvent : ChaosEvent
    {
        [Range(0f, 1f)] [SerializeField] private float beansChance = 0.5f;
        [SerializeField] private float draftInterval = 20f;
        [SerializeField] private float draftReputation = -2f;

        public float BeansChance => beansChance;
        public float DraftInterval => draftInterval;
        public float DraftReputation => draftReputation;

        public static WindowRockEvent CreateDefault()
        {
            var e = CreateInstance<WindowRockEvent>();
            e.Configure("WindowRock", "Rock through the window", "Something just came through the front window.", 1f, 1, 240f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<WindowRockRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="WindowRockEvent"/>: the throw, the hit, then the draft until the pane is in.</summary>
    public sealed class WindowRockRunner : ChaosEventRunner
    {
        private static readonly Color RockColor = new Color(0.45f, 0.43f, 0.4f);
        private static readonly Color BeansColor = new Color(0.55f, 0.35f, 0.15f);
        private static readonly Color CanColor = new Color(0.75f, 0.75f, 0.78f);

        private WindowRockEvent _event;
        private bool _beans;
        private bool _landed;
        private float _draftTimer;

        protected override void OnBegin()
        {
            _event = Definition as WindowRockEvent;
            _beans = Ctx.Rng.NextDouble() < (_event != null ? _event.BeansChance : 0.5f);
            _draftTimer = _event != null ? _event.DraftInterval : 20f;

            if (Ctx.Window != null)
            {
                Ctx.Window.Break();
                Ctx.Window.Repaired += OnWindowFixed;
            }

            // Seated diners only: a dasher or a held customer would be paid and knocked out twice over.
            CustomerAgent target = ChaosActors.PickSeatedCustomer(Ctx);
            Vector3 from = new Vector3(Ctx.WindowPoint.x, 1.6f, Ctx.WindowPoint.z);
            Vector3 fallback = ChaosActors.SampleNavMesh(Ctx.FloorBounds.center, 5f);
            GameEvents.RaiseNotice(_beans
                ? "A can of beans came through the front window. It is turning in the air like it's choosing. (audio cue: glass, then a whistle)"
                : "A rock came through the front window. It has picked someone. (audio cue: glass, then a whistle)");

            ThrownObject.Launch(_beans ? PrimitiveType.Cylinder : PrimitiveType.Sphere, _beans ? 0.22f : 0.2f, _beans ? CanColor : RockColor,
                from, fallback, target, 9f, 1.5f, OnLanded);
        }

        private void Update()
        {
            if (IsFinished || !_landed) return;
            if (Ctx.Window == null || !Ctx.Window.IsBroken)
            {
                Finish(true, "Window fixed");
                return;
            }

            _draftTimer -= Time.deltaTime;
            if (_draftTimer > 0f) return;
            _draftTimer = _event != null ? Mathf.Max(5f, _event.DraftInterval) : 20f;
            GameEvents.RaiseReputationNudged(_event != null ? _event.DraftReputation : -2f, "the broken window");
        }

        private void OnLanded(ThrownObject thrown, Interaction.IThrowTarget hit, Vector3 at)
        {
            if (this == null || IsFinished) return; // the throw outlives an aborted runner
            _landed = true;
            var customer = hit as CustomerAgent;
            if (customer != null && !customer.IsKnockedOut)
            {
                bool paid = customer.PayNow();
                GameEvents.RaiseNotice(paid
                    ? $"{(_beans ? "The beans" : "The rock")} found {customer.CustomerName}. Out cold. The bill paid itself and the table is clear."
                    : $"{(_beans ? "The beans" : "The rock")} found {customer.CustomerName}. Out cold.");
                customer.KnockOut(Ctx.Config != null ? Ctx.Config.KnockoutSeconds : 25f);
            }
            else
            {
                GameEvents.RaiseNotice(_beans ? "The beans found nobody and hit the floor." : "The rock found nobody and skidded under a table.");
            }

            if (_beans)
            {
                SpillPuddle puddle = SpillPuddle.Spawn("Bean Spill", null, Ctx, at, BeansColor, 1.5f, null);
                puddle.transform.SetParent(transform.parent, true); // outlives this runner; mopped like any spill
                GameEvents.RaiseNotice("The can burst. There are beans under the table now, and they are yours.");
            }
            else
            {
                PocketPickup.Spawn(PocketItems.Rock, "rock", 1, PlayerThrower.RockMax, at, RockColor, PrimitiveType.Sphere, 0.2f, keepOvernight: true);
                GameEvents.RaiseNotice("The rock is lying there. Pick it up (E): one throw, it homes on a seated customer, then it's a rock again.");
            }

            if (Ctx.Window == null)
            {
                Finish(true, "Someone got rocked");
                return;
            }
            WaitingOnPlayer = true; // only the pane is left; the scheduler can roll other events meanwhile
        }

        private void OnWindowFixed()
        {
            if (!IsFinished) Finish(true, "Window fixed");
        }

        private void OnDestroy()
        {
            if (Ctx != null && Ctx.Window != null) Ctx.Window.Repaired -= OnWindowFixed;
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
