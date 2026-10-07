using System.Collections;
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
    /// The pachislo cabinet by the front door. A pull costs your own money, never the till; the
    /// outcome rolls first and the reels land on it. Quarters spray on the floor as pickups for
    /// anyone's wallet, cookies go in your apron pocket. The machine keeps 35% in a cash box the
    /// leasing company never emptied; the wall of fortune pops its lock.
    /// </summary>
    public sealed class SlotMachine : MonoBehaviour, IInteractable
    {
        private enum Outcome { Nothing, Cookie, EggRolls, Cats, Dragons, Eights }

        private static readonly Color BlankColor = new Color(0.9f, 0.88f, 0.8f);
        private static readonly Color CookieColor = new Color(0.9f, 0.7f, 0.35f);
        private static readonly Color EggRollColor = new Color(0.85f, 0.55f, 0.2f);
        private static readonly Color CatColor = new Color(0.98f, 0.98f, 0.95f);
        private static readonly Color DragonColor = new Color(0.2f, 0.7f, 0.3f);
        private static readonly Color EightColor = new Color(0.95f, 0.15f, 0.15f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.2f);
        private static readonly Color[] SpinColors = { BlankColor, CookieColor, EggRollColor, CatColor, DragonColor, EightColor };

        [SerializeField] private EconomyConfig config;
        [SerializeField] private Renderer[] reels = new Renderer[0];
        [SerializeField] private Transform cat;
        [SerializeField] private Renderer catBody;
        [SerializeField] private TextMesh label;
        [SerializeField] private Transform traySpot;
        [SerializeField] private GameObject happyHourSign;

        private System.Random _rng = new System.Random();
        private float _cashBox;
        private bool _spinning;
        private bool _unlocked;
        private bool _happyHour;
        private float _catWave;
        private PlayerPocket _lastPocket;

        public float CashBox => _cashBox;

        private void OnEnable()
        {
            GameEvents.SlotJackpotUnlocked += OnJackpotUnlocked;
            GameEvents.DayClockTicked += OnClockTicked;
        }

        private void OnDisable()
        {
            GameEvents.SlotJackpotUnlocked -= OnJackpotUnlocked;
            GameEvents.DayClockTicked -= OnClockTicked;
        }

        public void Initialize(EconomyConfig economyConfig, System.Random rng, Renderer[] reelRenderers, Transform luckyCat, Renderer luckyCatBody, TextMesh statusLabel, Transform tray, GameObject happySign)
        {
            config = economyConfig;
            _rng = rng ?? new System.Random();
            reels = reelRenderers ?? new Renderer[0];
            cat = luckyCat;
            catBody = luckyCatBody;
            label = statusLabel;
            traySpot = tray;
            happyHourSign = happySign;
            _cashBox = config != null ? config.SlotCashBoxStart : 412f;
            if (happyHourSign != null) happyHourSign.SetActive(false);
            SetReels(BlankColor, BlankColor, BlankColor);
            RefreshLabel();
        }

        private float PullCost => _happyHour ? (config != null ? config.SlotHappyHourCost : 3f) : (config != null ? config.SlotPullCost : 5f);

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_spinning) return "The reels are spinning. The cat is waving faster.";
            return $"[E] Pull the lever  (${PullCost:0.00} from your wallet{(_happyHour ? ", HAPPY HOUR" : "")})";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_spinning) return;
            float cost = PullCost;
            var request = new WalletSpendRequest(cost, "a pull on the slot machine", transform.position);
            GameEvents.RaiseWalletSpendRequested(request);
            if (!request.Approved) return;

            _cashBox += cost * 0.35f;
            _lastPocket = inventory != null ? inventory.GetComponent<PlayerPocket>() : null;
            StartCoroutine(Spin(Roll()));
        }

        /// <summary>The outcome rolls first; the reels are cosmetic.</summary>
        private Outcome Roll()
        {
            double r = _rng.NextDouble();
            float cookie = config != null ? (_unlocked ? config.SlotCookieChanceAfterWall : config.SlotCookieChance) : 0.3f;
            float eggRolls = config != null ? config.SlotEggRollChance : 0.1f;
            float cats = config != null ? config.SlotCatChance : 0.05f;
            float dragons = config != null ? config.SlotDragonChance : 0.02f;
            float eights = config != null ? config.SlotEightsChance : 0.005f;

            // Rarest first so the big ones never get crowded out by a generous cookie chance.
            if (r < eights) return Outcome.Eights;
            r -= eights;
            if (r < dragons) return Outcome.Dragons;
            r -= dragons;
            if (r < cats) return Outcome.Cats;
            r -= cats;
            if (r < eggRolls) return Outcome.EggRolls;
            r -= eggRolls;
            if (r < cookie) return Outcome.Cookie;
            return Outcome.Nothing;
        }

        private IEnumerator Spin(Outcome outcome)
        {
            _spinning = true;
            RefreshLabel();
            GameEvents.RaiseNotice("Lever pulled. (audio cue: reels, and a lucky cat picking up the pace)");
            float seconds = config != null ? Mathf.Max(0.5f, config.SlotSpinSeconds) : 3f;
            float elapsed = 0f;
            float flicker = 0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                flicker -= dt;
                _catWave = Mathf.Lerp(4f, 14f, elapsed / seconds);
                if (flicker <= 0f)
                {
                    flicker = 0.08f;
                    for (int i = 0; i < reels.Length; i++)
                    {
                        // Reels stop left to right so the last one is the tease.
                        if (elapsed > seconds * (0.55f + 0.15f * i)) continue;
                        if (reels[i] != null) reels[i].sharedMaterial = MaterialLibrary.Get(SpinColors[_rng.Next(SpinColors.Length)]);
                    }
                }
                yield return null;
            }
            _catWave = 0f;
            Land(outcome);
            _spinning = false;
            RefreshLabel();
        }

        private void Land(Outcome outcome)
        {
            Vector3 tray = traySpot != null ? traySpot.position : transform.position + Vector3.up * 0.5f;
            switch (outcome)
            {
                case Outcome.Nothing:
                {
                    Color a = SpinColors[_rng.Next(SpinColors.Length)];
                    Color b = SpinColors[_rng.Next(SpinColors.Length)];
                    Color c = SpinColors[_rng.Next(SpinColors.Length)];
                    if (a == b && b == c) c = BlankColor;
                    SetReels(a, b, c);
                    GameEvents.RaiseNotice("Nothing. The cat keeps waving.");
                    break;
                }
                case Outcome.Cookie:
                    SetReels(CookieColor, SpinColors[_rng.Next(SpinColors.Length)], BlankColor);
                    DropCookie(tray);
                    break;
                case Outcome.EggRolls:
                {
                    float payout = config != null ? config.SlotEggRollPayout : 5f;
                    SetReels(EggRollColor, EggRollColor, EggRollColor);
                    CoinPickup.Burst(tray, payout, Mathf.Max(4, Mathf.RoundToInt(payout / 0.25f)), "egg roll x3 on the slot machine", null, true);
                    GameEvents.RaiseNotice($"Egg roll, egg roll, egg roll: ${payout:0.00} back, in quarters, on the floor.");
                    break;
                }
                case Outcome.Cats:
                {
                    float payout = config != null ? config.SlotCatPayout : 15f;
                    SetReels(CatColor, CatColor, CatColor);
                    CoinPickup.Burst(tray, payout, 24, "cat x3 on the slot machine", null, true);
                    GameEvents.RaiseNotice($"Three cats. ${payout:0.00} in quarters sprays across the floor. The real cat waves at you specifically.");
                    break;
                }
                case Outcome.Dragons:
                {
                    float payout = config != null ? config.SlotDragonPayout : 50f;
                    SetReels(DragonColor, DragonColor, DragonColor);
                    CoinPickup.Burst(tray, payout, 36, "dragon x3 on the slot machine", null, true);
                    GameEvents.RaiseNotice($"DRAGON DRAGON DRAGON. ${payout:0.00} in quarters. (audio cue: a two-second siren nobody installed)");
                    break;
                }
                case Outcome.Eights:
                {
                    float payout = config != null ? config.SlotEightsPayout : 200f;
                    SetReels(EightColor, EightColor, EightColor);
                    CoinPickup.Burst(tray, payout, 60, "8-8-8 on the slot machine", null, true);
                    GameEvents.RaiseLightingCueRequested(new Color(1f, 0.15f, 0.1f), 30f);
                    GameEvents.RaiseNotice($"8 8 8. ${payout:0.00}. The lights go red. Every customer in the building turns to look at you.");
                    break;
                }
            }
        }

        private void DropCookie(Vector3 tray)
        {
            int limit = config != null ? config.PocketCookieLimit : 5;
            if (_lastPocket != null && _lastPocket.Add(PocketItems.Cookie, "fortune cookie", 1, limit) > 0)
            {
                GameEvents.RaiseNotice($"A fortune cookie dropped into the tray and went in your pocket ({_lastPocket.Count(PocketItems.Cookie)}/{limit}). R to crack it, or throw it at someone.");
                return;
            }
            PocketPickup.Spawn(PocketItems.Cookie, "fortune cookie", 1, limit, tray + new Vector3(0.3f, 0f, 0.3f), CookieColor, PrimitiveType.Sphere, 0.18f);
            GameEvents.RaiseNotice("A fortune cookie dropped into the tray. Your pocket is full of them; it's on the floor.");
        }

        private void OnJackpotUnlocked()
        {
            if (_unlocked) return;
            _unlocked = true;
            Vector3 tray = traySpot != null ? traySpot.position : transform.position + Vector3.up * 0.5f;
            float spill = _cashBox;
            _cashBox = 0f;
            if (catBody != null) catBody.sharedMaterial = MaterialLibrary.Get(GoldColor);
            CoinPickup.Burst(tray, spill, Mathf.Clamp(Mathf.RoundToInt(spill / 2f), 20, 120), "the slot machine's cash box", null, true);
            GameEvents.RaiseNotice($"The slot machine's lock popped. ${spill:0.00} in quarters, every dollar it ever took plus the leasing company's, is on the floor. The cat is gold now.");
            RefreshLabel();
        }

        /// <summary>From 3 to 5 PM a paper plate reading HAPPY HOUR hangs on the lever.</summary>
        private void OnClockTicked(DayClockSnapshot snapshot)
        {
            if (snapshot.DayLengthSeconds <= 0f) return;
            float elapsed = 1f - Mathf.Clamp01(snapshot.SecondsRemaining / snapshot.DayLengthSeconds);
            // The day runs 10:30 AM to 9:00 PM; 3 PM is 4.5 h in, 5 PM is 6.5 h in, of 10.5 h.
            bool happy = snapshot.Phase == DayPhase.Open && elapsed >= 4.5f / 10.5f && elapsed < 6.5f / 10.5f;
            if (happy == _happyHour) return;
            _happyHour = happy;
            if (happyHourSign != null) happyHourSign.SetActive(happy);
            if (happy) GameEvents.RaiseNotice("A paper plate reading HAPPY HOUR is hanging on the slot machine's lever. Pulls are cheaper until 5.");
            RefreshLabel();
        }

        private void Update()
        {
            if (cat == null) return;
            float rate = _catWave > 0f ? _catWave : 2.5f;
            cat.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * rate) * 25f);
        }

        private void SetReels(Color a, Color b, Color c)
        {
            if (reels.Length > 0 && reels[0] != null) reels[0].sharedMaterial = MaterialLibrary.Get(a);
            if (reels.Length > 1 && reels[1] != null) reels[1].sharedMaterial = MaterialLibrary.Get(b);
            if (reels.Length > 2 && reels[2] != null) reels[2].sharedMaterial = MaterialLibrary.Get(c);
        }

        private void RefreshLabel()
        {
            if (label == null) return;
            label.text = _spinning ? "SLOT MACHINE\nspinning" : $"SLOT MACHINE\n${PullCost:0} a pull{(_unlocked ? " (cookies 50%)" : "")}";
        }
    }
}
