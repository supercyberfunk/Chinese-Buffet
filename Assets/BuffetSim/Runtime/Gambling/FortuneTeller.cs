using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Economy;
using BuffetSim.Items;
using UnityEngine;

namespace BuffetSim.Gambling
{
    /// <summary>
    /// Listens for a cracked cookie, picks a fortune (one you haven't seen while any remain), applies
    /// its effect through the bus and announces the slip. It never touches the player or the ledger
    /// directly: effects are requests other systems answer.
    /// </summary>
    public sealed class FortuneTeller : MonoBehaviour
    {
        [SerializeField] private FortuneCatalog catalog;
        [SerializeField] private EconomyConfig config;

        private System.Random _rng = new System.Random();
        private Transform _player;
        private readonly HashSet<int> _revealed = new HashSet<int>();

        public FortuneCatalog Catalog => catalog;

        private void OnEnable()
        {
            GameEvents.FortuneCookieCracked += OnCracked;
        }

        private void OnDisable()
        {
            GameEvents.FortuneCookieCracked -= OnCracked;
        }

        public void Initialize(FortuneCatalog fortuneCatalog, EconomyConfig economyConfig, System.Random rng, Transform player)
        {
            catalog = fortuneCatalog;
            config = economyConfig;
            _rng = rng ?? new System.Random();
            _player = player;
        }

        private void OnCracked(Vector3 at)
        {
            if (catalog == null || catalog.Count == 0)
            {
                GameEvents.RaiseNotice("The cookie was empty. Somebody at the factory is having a day.");
                return;
            }

            FortuneEntry entry = Pick();
            _revealed.Add(entry.Id);
            string summary = Apply(entry, at);
            GameEvents.RaiseFortuneRevealed(new FortuneReveal { Id = entry.Id, Text = entry.Text, Kind = entry.Kind, EffectSummary = summary });
            GameEvents.RaiseNotice(string.IsNullOrEmpty(summary) ? $"Fortune: \"{entry.Text}\"" : $"Fortune: \"{entry.Text}\"  {summary}");
        }

        /// <summary>Until every slip has been seen, a cookie never repeats; after that anything goes, and a duplicate still works.</summary>
        private FortuneEntry Pick()
        {
            IReadOnlyList<FortuneEntry> all = catalog.Fortunes;
            var unseen = new List<FortuneEntry>();
            for (int i = 0; i < all.Count; i++)
                if (!_revealed.Contains(all[i].Id)) unseen.Add(all[i]);
            IReadOnlyList<FortuneEntry> pool = unseen.Count > 0 ? unseen : all;
            return pool[_rng.Next(pool.Count)];
        }

        private string Apply(FortuneEntry entry, Vector3 at)
        {
            string effect = entry.EffectId ?? "none";
            if (effect.StartsWith("event:"))
            {
                GameEvents.RaiseChaosEventRequested(effect.Substring("event:".Length));
                return entry.Summary;
            }

            switch (effect)
            {
                case "sprint-off":
                    Effect(PlayerEffectKind.SprintDisabled, entry.Seconds, 0f);
                    break;
                case "drop":
                    Effect(PlayerEffectKind.DropEverything, 0f, 0f);
                    break;
                case "small-hands":
                    Effect(PlayerEffectKind.PlateCapacity, entry.Seconds, 2f);
                    Effect(PlayerEffectKind.FoodCapacity, entry.Seconds, 10f);
                    break;
                case "maxed-orders":
                    GameEvents.RaiseOrderBoostRequested(Mathf.Max(1, entry.Count));
                    break;
                case "follower":
                    if (_player != null) GameEvents.RaiseCustomerFollowRequested(_player, entry.Seconds);
                    break;
                case "comp-next":
                    GameEvents.RaiseBillModifierRequested(new BillModifier { Kind = BillModifierKind.CompNext, Count = Mathf.Max(1, entry.Count), Source = "a fortune" });
                    break;
                case "slow-cookers":
                    GameEvents.RaiseCookingSpeedRequested(entry.Value > 0f ? entry.Value : 0.5f, entry.Seconds);
                    break;
                case "slip-every":
                    Effect(PlayerEffectKind.SlipEvery, entry.Seconds, entry.Value > 0f ? entry.Value : 15f);
                    break;
                case "lose-cash":
                {
                    float amount = entry.Value > 0f ? entry.Value : 5f;
                    var request = new WalletSpendRequest(amount, "a hole in your pocket", at);
                    GameEvents.RaiseWalletSpendRequested(request);
                    if (!request.Approved) return "Your pocket had nothing in it to lose. A first.";
                    CoinPickup.Burst(at, amount, Mathf.Max(4, Mathf.RoundToInt(amount / 0.25f)), "quarters from your own pocket", null, true);
                    break;
                }
                case "fast-feet":
                    Effect(PlayerEffectKind.SprintMultiplier, entry.Seconds, entry.Value > 0f ? entry.Value : 1.5f);
                    Effect(PlayerEffectKind.NoSlip, entry.Seconds, 0f);
                    break;
                case "big-hands":
                    Effect(PlayerEffectKind.PlateCapacity, entry.Seconds, entry.Value > 0f ? entry.Value : 8f);
                    break;
                case "old-debt":
                {
                    float amount = entry.Value > 0f ? entry.Value : 20f;
                    CoinPickup.Burst(at, amount, 24, "an old debt, in quarters", null, true);
                    break;
                }
                case "boost-next":
                    GameEvents.RaiseBillModifierRequested(new BillModifier { Kind = BillModifierKind.BoostNext, Count = Mathf.Max(1, entry.Count), Multiplier = entry.Value > 0f ? entry.Value : 1.25f, Source = "a fortune" });
                    break;
                case "dishwasher":
                    GameEvents.RaiseDishwasherRunRequested();
                    break;
                case "tray-refill":
                    GameEvents.RaiseTrayRefillRequested(at);
                    break;
                case "dashers-trip":
                    GameEvents.RaiseDashersTripRequested(entry.Seconds);
                    break;
                case "forgive":
                    GameEvents.RaiseBillModifierRequested(new BillModifier { Kind = BillModifierKind.ForgiveMistakes, Seconds = entry.Seconds, Source = "a fortune" });
                    break;
                default:
                    return string.Empty;
            }
            return entry.Summary;
        }

        private void Effect(PlayerEffectKind kind, float seconds, float value)
        {
            GameEvents.RaisePlayerEffectRequested(new PlayerEffect { Kind = kind, Seconds = seconds, Value = value, Source = "a fortune" });
        }
    }
}
