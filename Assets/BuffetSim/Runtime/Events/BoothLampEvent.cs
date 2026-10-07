using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// The lamp over one table goes out. Just that one; the customer under it stops eating and sits
    /// in the dark looking unwell. Fetch a lightbulb from the maintenance shelf and hold E at the
    /// lamp: the light comes back and the customer, grateful, pays 10% more. Leave it and they walk,
    /// unpaid, taking some satisfaction with them.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Booth Lamp", fileName = "BoothLamp")]
    public sealed class BoothLampEvent : ChaosEvent
    {
        [SerializeField] private float patienceSeconds = 75f;
        [SerializeField] private float holdSeconds = 3f;
        [SerializeField] private float billBonus = 0.1f;
        [SerializeField] private float reputationIfIgnored = -6f;
        [SerializeField] private float darkInterval = 15f;
        [SerializeField] private float darkReputation = -1f;

        public float PatienceSeconds => patienceSeconds;
        public float HoldSeconds => holdSeconds;
        public float BillBonus => billBonus;
        public float ReputationIfIgnored => reputationIfIgnored;
        public float DarkInterval => darkInterval;
        public float DarkReputation => darkReputation;

        public static BoothLampEvent CreateDefault()
        {
            var e = CreateInstance<BoothLampEvent>();
            e.Configure("BoothLamp", "Booth lamp", "A lamp went out over one table. The customer under it is not okay.", 1f, 1, 200f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<BoothLampRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="BoothLampEvent"/>: one dark lamp, one frightened customer, one lightbulb.</summary>
    public sealed class BoothLampRunner : ChaosEventRunner
    {
        private static readonly Color DeadBulb = new Color(0.2f, 0.2f, 0.22f);
        private static readonly string[] Statuses = { "Sitting in the dark", "Can't see the food", "Whispering to the lamp", "This is how it ends" };

        private BoothLampEvent _event;
        private CustomerAgent _customer;
        private Renderer _lamp;
        private Material _lampMaterial;
        private RepairPoint _repair;
        private float _patience;
        private float _darkTimer;
        private float _statusTimer;

        protected override void OnBegin()
        {
            _event = Definition as BoothLampEvent;
            _customer = ChaosActors.PickCustomer(Ctx, c => c.IsSeated && !c.IsPaid && !c.IsHeld && !c.IsKnockedOut && c.Table != null && c.Table.Lamp != null);
            if (_customer == null)
            {
                GameEvents.RaiseNotice("A lamp flickered over an empty table and thought better of it.");
                Finish(true, "Nobody under the lamp");
                return;
            }

            _lamp = _customer.Table.Lamp;
            _lampMaterial = _lamp.sharedMaterial;
            _lamp.sharedMaterial = MaterialLibrary.Get(DeadBulb);
            _customer.Hold(Statuses[0]);
            _patience = _event != null ? _event.PatienceSeconds : 75f;
            _darkTimer = _event != null ? _event.DarkInterval : 15f;
            _statusTimer = 8f;

            Vector3 lampPosition = _lamp.transform.position;
            _repair = RepairPoint.Create("Repair - Lamp", transform, lampPosition, new Vector3(0.9f, 0.9f, 0.9f),
                CarryItems.Lightbulb, "a lightbulb", _event != null ? _event.HoldSeconds : 3f, "Change the lightbulb", "the maintenance shelf in the kitchen", OnFixed);
            GameEvents.RaiseNotice($"The lamp over {_customer.CustomerName}'s table went out. They've gone very quiet. Lightbulbs are on the maintenance shelf. (audio cue: a filament pinging)");
        }

        private void Update()
        {
            if (IsFinished) return;
            if (_customer == null)
            {
                RestoreLamp();
                Finish(false, "The customer under the dead lamp is gone");
                return;
            }

            float dt = Time.deltaTime;
            _statusTimer -= dt;
            if (_statusTimer <= 0f)
            {
                _statusTimer = 8f;
                _customer.SetHoldStatus(ChaosActors.Pick(Ctx.Rng, Statuses));
            }

            _darkTimer -= dt;
            if (_darkTimer <= 0f)
            {
                _darkTimer = _event != null ? Mathf.Max(5f, _event.DarkInterval) : 15f;
                GameEvents.RaiseReputationNudged(_event != null ? _event.DarkReputation : -1f, "a dark table");
            }

            _patience -= dt;
            if (_patience <= 0f) GiveUp();
        }

        private void OnFixed()
        {
            if (IsFinished || _customer == null) return;
            RestoreLamp();
            float bonus = _event != null ? _event.BillBonus : 0.1f;
            _customer.ApplyBillMultiplier(1f + bonus);
            _customer.ReleaseHold();
            GameEvents.RaiseNotice($"Light. {_customer.CustomerName} blinks, says nothing about it, and will tip {bonus:P0} for the trauma.");
            Finish(true, $"Lamp fixed, +{bonus:P0} on {_customer.CustomerName}'s bill");
        }

        private void GiveUp()
        {
            if (IsFinished) return;
            RestoreLamp();
            string name = _customer.CustomerName;
            _customer.LeaveWithoutPaying($"{name} walked out of the dark without paying. The lamp came back on as they reached the door, obviously.");
            GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationIfIgnored : -6f, "left in the dark");
            Finish(false, $"{name} left unpaid");
        }

        private void RestoreLamp()
        {
            if (_lamp != null && _lampMaterial != null) _lamp.sharedMaterial = _lampMaterial;
        }

        public override void Abort()
        {
            RestoreLamp();
            if (_customer != null) _customer.ReleaseHold();
            EndSilently();
        }
    }
}
