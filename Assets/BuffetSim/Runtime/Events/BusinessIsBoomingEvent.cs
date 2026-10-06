using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Customers;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// Half the line turns into suits who pay a multiplied bill, and new arrivals keep getting the
    /// same coin flip for a while. Pure upside; nothing for the player to do but enjoy it.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Business Is Booming", fileName = "BusinessIsBooming")]
    public sealed class BusinessIsBoomingEvent : ChaosEvent
    {
        [SerializeField] private float billMultiplier = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float promoteChance = 0.5f;
        [SerializeField] private float durationSeconds = 90f;
        [Tooltip("How often newcomers in the line are checked for promotion.")]
        [SerializeField] private float sweepInterval = 2f;

        public float BillMultiplier => billMultiplier;
        public float PromoteChance => promoteChance;
        public float DurationSeconds => durationSeconds;
        public float SweepInterval => sweepInterval;

        public static BusinessIsBoomingEvent CreateDefault()
        {
            var e = CreateInstance<BusinessIsBoomingEvent>();
            e.Configure("BusinessIsBooming", "Business is booming", "Half the line just turned into suits. They tip.", 1f, 1, 120f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<BusinessIsBoomingRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="BusinessIsBoomingEvent"/>: promotes the line now and newcomers for the duration.</summary>
    public sealed class BusinessIsBoomingRunner : ChaosEventRunner
    {
        private readonly HashSet<CustomerAgent> _seen = new HashSet<CustomerAgent>();
        private float _multiplier = 1.5f;
        private float _chance = 0.5f;
        private float _sweepInterval = 2f;
        private float _timeLeft = 90f;
        private float _sweepTimer;
        private int _suits;

        public int SuitsPromoted => _suits;

        protected override void OnBegin()
        {
            if (Definition is BusinessIsBoomingEvent e)
            {
                _multiplier = e.BillMultiplier;
                _chance = e.PromoteChance;
                _sweepInterval = Mathf.Max(0.25f, e.SweepInterval);
                _timeLeft = e.DurationSeconds;
            }
            _sweepTimer = _sweepInterval;

            GameEvents.RaiseNotice("Business is booming.");
            if (Ctx.Queue != null)
            {
                // PromoteToSuit never touches the queue, so iterating the live line is safe.
                IReadOnlyList<CustomerAgent> line = Ctx.Queue.Customers;
                for (int i = 0; i < line.Count; i++) Consider(line[i]);
            }
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;
            _timeLeft -= dt;
            _sweepTimer -= dt;
            if (_sweepTimer <= 0f)
            {
                _sweepTimer = _sweepInterval;
                Sweep();
            }
            if (_timeLeft <= 0f) Finish(true, $"{_suits} suit{(_suits == 1 ? "" : "s")} paid {_multiplier:0.#}x");
        }

        /// <summary>Newcomers still walking to or standing in the line get the same coin flip, once each.</summary>
        private void Sweep()
        {
            IReadOnlyList<CustomerAgent> inStore = Ctx.GetCustomersInStore();
            for (int i = 0; i < inStore.Count; i++)
            {
                CustomerAgent customer = inStore[i];
                if (customer == null) continue;
                CustomerAgent.State state = customer.CurrentState;
                if (state != CustomerAgent.State.WalkingToLine && state != CustomerAgent.State.WaitingInLine) continue;
                Consider(customer);
            }
        }

        private void Consider(CustomerAgent customer)
        {
            if (customer == null || !_seen.Add(customer)) return;
            if (customer.IsSuit) return;
            if (Ctx.Rng.NextDouble() >= _chance) return;
            customer.PromoteToSuit(_multiplier);
            if (customer.IsSuit) _suits++;
        }

        public override void Abort()
        {
            // Nothing to clean up: the suits stay suits.
            EndSilently();
        }
    }
}
