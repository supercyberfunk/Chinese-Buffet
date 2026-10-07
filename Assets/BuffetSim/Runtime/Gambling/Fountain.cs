using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Economy;
using BuffetSim.Interaction;
using BuffetSim.Items;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Gambling
{
    /// <summary>
    /// The wishing fountain. Paying customers flip a quarter in now and then; at close the basin
    /// opens for a few seconds and whoever holds E at the water scoops quarters into their own
    /// wallet, outside the split. What isn't scooped drains to the parking lot at the fade.
    /// </summary>
    public sealed class Fountain : MonoBehaviour, IInteractable, IHoldInteractable
    {
        private static readonly Color QuarterColor = new Color(0.8f, 0.82f, 0.85f);

        [SerializeField] private EconomyConfig config;
        [SerializeField] private Transform water;
        [SerializeField] private float basinRadius = 0.8f;
        [SerializeField] private TextMesh label;

        private System.Random _rng = new System.Random();
        private readonly List<GameObject> _coinVisuals = new List<GameObject>();
        private int _wishQuarters;
        private int _quarters;
        private bool _open;
        private float _openLeft;

        public bool IsOpen => _open;
        public int Quarters => _quarters;

        private void OnEnable()
        {
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.CustomerPaid += OnCustomerPaid;
        }

        private void OnDisable()
        {
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.CustomerPaid -= OnCustomerPaid;
        }

        public void Initialize(EconomyConfig economyConfig, System.Random rng, Transform waterSurface, float radius, TextMesh statusLabel)
        {
            config = economyConfig;
            _rng = rng ?? new System.Random();
            water = waterSurface;
            basinRadius = radius;
            label = statusLabel;
            RefreshLabel();
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_open)
            {
                return _quarters > 0
                    ? $"[Hold E] Scoop quarters from the fountain ({_quarters} left, {Mathf.CeilToInt(_openLeft)}s)"
                    : "The fountain is empty. Somebody was quicker.";
            }
            return _wishQuarters > 0
                ? $"Wishing fountain: {_wishQuarters} quarter{(_wishQuarters == 1 ? "" : "s")} wished in so far. Opens at close."
                : "Wishing fountain. Opens at close.";
        }

        public void Interact(PlayerInventory inventory) { }

        public float GetHoldSeconds(PlayerInventory inventory) => _open && _quarters > 0 ? 1f : 0f;

        public void CompleteHold(PlayerInventory inventory)
        {
            if (!_open || _quarters <= 0) return;
            int min = config != null ? Mathf.Max(1, config.FountainScoopMinCoins) : 1;
            int max = config != null ? Mathf.Max(min, config.FountainScoopMaxCoins) : 3;
            int scooped = Mathf.Min(_quarters, _rng.Next(min, max + 1));
            _quarters -= scooped;
            float value = config != null ? config.QuarterValue : 0.25f;
            GameEvents.RaiseWalletCredited(scooped * value, "fountain quarters", transform.position + Vector3.up);
            TrimCoinVisuals();
            if (_quarters <= 0) GameEvents.RaiseNotice("You scooped the fountain clean. The wishes are yours now, legally.");
            RefreshLabel();
        }

        private void Update()
        {
            if (!_open) return;
            _openLeft -= Time.deltaTime;
            if (_openLeft > 0f) return;
            _open = false;
            int lost = _quarters;
            _quarters = 0;
            ClearCoinVisuals();
            if (lost > 0) GameEvents.RaiseNotice($"The fountain drained. {lost} quarter{(lost == 1 ? "" : "s")} went to the parking lot.");
            RefreshLabel();
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            if (snapshot.Phase != DayPhase.Closed || _open) return;
            float min = config != null ? config.FountainMinPayout : 2f;
            float max = config != null ? config.FountainMaxPayout : 8f;
            float value = config != null ? Mathf.Max(0.01f, config.QuarterValue) : 0.25f;
            float payout = Mathf.Lerp(min, max, (float)_rng.NextDouble());
            _quarters = Mathf.Max(0, Mathf.RoundToInt(payout / value)) + _wishQuarters;
            _wishQuarters = 0;
            _open = true;
            _openLeft = config != null ? config.FountainOpenSeconds : 15f;
            ClearCoinVisuals();
            for (int i = 0; i < Mathf.Min(24, _quarters); i++) AddCoinVisual();
            GameEvents.RaiseNotice($"The fountain is open for {Mathf.RoundToInt(_openLeft)}s: hold E at the water to scoop {_quarters} quarters into your own wallet.");
            RefreshLabel();
        }

        private void OnDayStarted(int day)
        {
            _open = false;
            _quarters = 0;
            _wishQuarters = 0;
            ClearCoinVisuals();
            RefreshLabel();
        }

        /// <summary>One paying customer in four flips a quarter or two in on the way out.</summary>
        private void OnCustomerPaid(CustomerReceipt receipt)
        {
            if (receipt.Total <= 0f || _rng.NextDouble() > 0.25) return;
            int flipped = _rng.Next(1, 3);
            _wishQuarters += flipped;
            Vector3 target = transform.position + Vector3.up * 0.6f;
            ThrownObject.Launch(PrimitiveType.Cylinder, 0.12f, QuarterColor, receipt.WorldPosition + Vector3.up * 1.2f, target, null, 5f, 1.6f, (thrown, hit, at) => { });
            RefreshLabel();
        }

        private void AddCoinVisual()
        {
            Transform parent = water != null ? water.parent : transform;
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float radius = (float)_rng.NextDouble() * basinRadius * 0.85f;
            Vector3 local = new Vector3(Mathf.Cos(angle) * radius, water != null ? water.localPosition.y + 0.03f : 0.5f, Mathf.Sin(angle) * radius);
            GameObject coin = PrimitiveFactory.Visual("Fountain Quarter", PrimitiveType.Cylinder, parent, local, new Vector3(0.14f, 0.01f, 0.14f), MaterialLibrary.Get(QuarterColor));
            _coinVisuals.Add(coin);
        }

        private void TrimCoinVisuals()
        {
            int keep = Mathf.Min(24, _quarters);
            while (_coinVisuals.Count > keep)
            {
                GameObject coin = _coinVisuals[_coinVisuals.Count - 1];
                _coinVisuals.RemoveAt(_coinVisuals.Count - 1);
                if (coin != null) Destroy(coin);
            }
        }

        private void ClearCoinVisuals()
        {
            for (int i = 0; i < _coinVisuals.Count; i++)
                if (_coinVisuals[i] != null) Destroy(_coinVisuals[i]);
            _coinVisuals.Clear();
        }

        private void RefreshLabel()
        {
            if (label == null) return;
            label.text = _open ? $"FOUNTAIN OPEN\n{_quarters} quarters" : "WISHING\nFOUNTAIN";
        }
    }
}
