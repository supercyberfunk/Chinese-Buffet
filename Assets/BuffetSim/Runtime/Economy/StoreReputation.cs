using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Economy
{
    /// <summary>
    /// Store-wide customer satisfaction, clamped to 0..100. It listens to what customers do
    /// (paid in full, short-changed, walked out, slipped) and to anyone asking for a nudge, and
    /// publishes every change on the bus. Nothing else holds the value; the spawner and HUD listen.
    /// </summary>
    public sealed class StoreReputation : MonoBehaviour
    {
        [SerializeField] private EconomyConfig config;

        private float _value;
        private bool _initialized;

        public float Value => _value;

        public void Initialize(EconomyConfig economyConfig)
        {
            config = economyConfig;
            _value = Mathf.Clamp(config.StartingReputation, 0f, 100f);
            _initialized = true;
            GameEvents.RaiseReputationChanged(_value, 0f, "Opening");
        }

        private void OnEnable()
        {
            GameEvents.CustomerPaid += OnCustomerPaid;
            GameEvents.CustomerLost += OnCustomerLost;
            GameEvents.CustomerSlipped += OnCustomerSlipped;
            GameEvents.ReputationNudged += OnReputationNudged;
        }

        private void OnDisable()
        {
            GameEvents.CustomerPaid -= OnCustomerPaid;
            GameEvents.CustomerLost -= OnCustomerLost;
            GameEvents.CustomerSlipped -= OnCustomerSlipped;
            GameEvents.ReputationNudged -= OnReputationNudged;
        }

        private void OnCustomerPaid(CustomerReceipt receipt)
        {
            if (!_initialized) return;
            if (receipt.Deductions > 0f)
                Apply(config.ReputationShortChanged, $"{receipt.CustomerName} was short-changed");
            else
                Apply(config.ReputationPaidInFull, $"{receipt.CustomerName} paid in full");
        }

        private void OnCustomerLost(string customerName, Vector3 at)
        {
            if (!_initialized) return;
            Apply(config.ReputationWalkout, $"{customerName} walked out");
        }

        private void OnCustomerSlipped(string customerName, Vector3 at)
        {
            if (!_initialized) return;
            Apply(config.ReputationSlip, $"{customerName} slipped");
        }

        private void OnReputationNudged(float delta, string reason)
        {
            if (!_initialized) return;
            Apply(delta, reason);
        }

        /// <summary>Moves the value by <paramref name="delta"/> inside 0..100 and publishes the delta that actually applied.</summary>
        private void Apply(float delta, string reason)
        {
            float before = _value;
            _value = Mathf.Clamp(_value + delta, 0f, 100f);
            GameEvents.RaiseReputationChanged(_value, _value - before, reason ?? string.Empty);
        }
    }
}
