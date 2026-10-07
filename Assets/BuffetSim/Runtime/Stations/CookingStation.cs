using System.Text;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Economy;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Stations
{
    public enum CookerState
    {
        Empty,
        /// <summary>A raw box is in, nothing is on yet.</summary>
        Loaded,
        /// <summary>Wok only: the flip minigame before the burner timer starts.</summary>
        Flipping,
        Cooking,
        /// <summary>Cooked and waiting to be taken out; the burn clock is running.</summary>
        Done,
        /// <summary>Past the grace period: units are being lost.</summary>
        Burning,
    }

    /// <summary>
    /// One cooker in the kitchen: the deep fryer, the wok, the steamer or the rice cooker. Load a raw
    /// box of a food that belongs to it, start it the way that cooker starts (lower the basket, flip
    /// the wok, set a dial, press COOK), wait, and take the cooked tray out before it burns. Each
    /// cooker is its own component with its own state; the only thing it tells the rest of the
    /// game is "cooked food went to the cooler", over the bus.
    /// </summary>
    public sealed class CookingStation : MonoBehaviour, IInteractable, IHoldInteractable, ISecondaryInteractable
    {
        private const int GaugeSegments = 24;
        private const float GaugeSweepsPerSecond = 0.8f;

        [SerializeField] private CookerKind kind = CookerKind.Fryer;
        [SerializeField] private string displayName = "Cooker";
        [SerializeField] private EconomyConfig config;

        [Header("Visuals (optional)")]
        [SerializeField] private TextMesh label;
        [Tooltip("Scaled on Y with the units inside and tinted with the food's colour.")]
        [SerializeField] private Transform foodVisual;
        [SerializeField] private Renderer indicator;
        [Tooltip("Fryer: lowered while cooking. Anything else: left alone.")]
        [SerializeField] private Transform basketVisual;
        [SerializeField] private Transform smokeVisual;

        private static readonly Color IndicatorOff = new Color(0.25f, 0.25f, 0.27f);
        private static readonly Color IndicatorOn = new Color(1f, 0.45f, 0.1f);
        private static readonly Color IndicatorDone = new Color(0.3f, 1f, 0.35f);
        private static readonly Color IndicatorBurn = new Color(0.1f, 0.05f, 0.05f);

        private FoodDefinition _food;
        private int _units;
        private CookerState _state = CookerState.Empty;
        private float _cookTotal;
        private float _cookRemaining;
        private float _doneSeconds;
        private float _burnTimer;
        private int _flips;
        private int _batchSize;
        private float _gaugePhase;
        private float _speedMultiplier = 1f;
        private float _speedUntil = float.NegativeInfinity;
        private float _foodVisualHeight = 0.2f;
        private Vector3 _basketUp;
        private float _basketDrop = 0.25f;
        private Vector3 _foodBase;
        private float _bounce;

        public CookerKind Kind => kind;
        public CookerState State => _state;
        public FoodDefinition Food => _food;
        public int Units => _units;

        private void OnEnable()
        {
            GameEvents.CookingSpeedRequested += OnSpeedRequested;
            GameEvents.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            GameEvents.CookingSpeedRequested -= OnSpeedRequested;
            GameEvents.DayEnded -= OnDayEnded;
        }

        public void Configure(CookerKind cookerKind, string cookerName, EconomyConfig economyConfig, TextMesh statusLabel,
            Transform food, float foodFullHeight, Renderer light, Transform basket, float basketDrop, Transform smoke)
        {
            kind = cookerKind;
            displayName = cookerName;
            config = economyConfig;
            label = statusLabel;
            foodVisual = food;
            _foodVisualHeight = foodFullHeight;
            indicator = light;
            basketVisual = basket;
            _basketDrop = basketDrop;
            smokeVisual = smoke;
            if (basketVisual != null) _basketUp = basketVisual.localPosition;
            if (foodVisual != null) _foodBase = foodVisual.localPosition;
            RefreshVisuals();
        }

        // ----- Prompts -----

        public string GetPrompt(PlayerInventory inventory)
        {
            string foodName = _food != null ? _food.DisplayName : string.Empty;
            switch (_state)
            {
                case CookerState.Empty:
                    if (inventory.IsHoldingRawFood)
                    {
                        return inventory.HeldFood.Cooker == kind
                            ? $"[E] Load {inventory.HeldFoodUnits} raw {inventory.HeldFood.DisplayName} into the {displayName}"
                            : $"{displayName}: {inventory.HeldFood.DisplayName} goes in the {inventory.HeldFood.Cooker.DisplayName()}";
                    }
                    if (inventory.IsHoldingCookedFood) return $"{displayName}: that {inventory.HeldFood.DisplayName} is already cooked";
                    return $"{displayName} (empty) - bring a raw box from the cooler";

                case CookerState.Loaded:
                    switch (kind)
                    {
                        case CookerKind.Fryer: return $"[Hold E] Lower the basket ({_units} {foodName})";
                        case CookerKind.Wok: return $"[E] Light the burner ({_units} {foodName})";
                        case CookerKind.Steamer: return $"[E] Set the dial ({_units} {foodName})";
                        default: return $"[E] Press COOK ({_units} {foodName})";
                    }

                case CookerState.Flipping:
                    return $"[E] Flip when the mark is in the hot zone  ({_flips}/{FlipsNeeded} flips, {_units} {foodName})\n{Gauge()}";

                case CookerState.Cooking:
                    return $"{displayName}: {foodName} cooking, {Mathf.CeilToInt(_cookRemaining)}s{(SpeedActive ? $" (x{_speedMultiplier:0.#} speed)" : "")}";

                case CookerState.Done:
                {
                    float grace = config != null ? config.BurnGraceSeconds : 30f;
                    float left = Mathf.Max(0f, grace - _doneSeconds);
                    string warning = left <= 10f ? $"  (burns in {Mathf.CeilToInt(left)}s)" : string.Empty;
                    return inventory.HandsFree
                        ? $"[E] Take the {_units} cooked {foodName}{warning}"
                        : $"{displayName}: {_units} {foodName} done - hands full{warning}";
                }

                case CookerState.Burning:
                    return inventory.HandsFree
                        ? $"[E] Take the {_units} {foodName} before it's gone  (BURNING)"
                        : $"{displayName}: {foodName} is BURNING ({_units} left) - free your hands";
            }
            return displayName;
        }

        public string GetSecondaryPrompt(PlayerInventory inventory)
        {
            switch (_state)
            {
                case CookerState.Loaded: return inventory.HandsFree ? "[Q] Take the raw box back out" : string.Empty;
                case CookerState.Flipping: return inventory.HandsFree ? "[Q] Burner off, take the box back out" : string.Empty;
                case CookerState.Cooking: return $"[Q] Dump the batch (loses {_units} units)";
                case CookerState.Done:
                case CookerState.Burning: return "[Q] Send it to the cooler";
                default: return string.Empty;
            }
        }

        // ----- Keys -----

        public void Interact(PlayerInventory inventory)
        {
            switch (_state)
            {
                case CookerState.Empty:
                    Load(inventory);
                    break;
                case CookerState.Loaded:
                    if (kind == CookerKind.Wok) BeginFlipping();
                    else if (kind != CookerKind.Fryer) StartCooking();
                    break;
                case CookerState.Flipping:
                    AttemptFlip();
                    break;
                case CookerState.Done:
                case CookerState.Burning:
                    TakeOut(inventory);
                    break;
            }
        }

        public float GetHoldSeconds(PlayerInventory inventory)
        {
            if (_state == CookerState.Loaded && kind == CookerKind.Fryer) return config != null ? config.FryerBasketSeconds : 1.5f;
            return 0f;
        }

        public void CompleteHold(PlayerInventory inventory)
        {
            if (_state == CookerState.Loaded && kind == CookerKind.Fryer) StartCooking();
        }

        public void SecondaryInteract(PlayerInventory inventory)
        {
            switch (_state)
            {
                case CookerState.Loaded:
                case CookerState.Flipping:
                {
                    // A fortune can cap what your hands manage; whatever doesn't fit stays raw in the cooker.
                    int taken = Mathf.Min(_units, inventory.FoodCapacity);
                    if (taken <= 0 || !inventory.TryTakeFoodTray(_food, taken, true)) break;
                    _units -= taken;
                    if (_units > 0)
                    {
                        GameEvents.RaiseNotice($"Took {taken} raw {_food.DisplayName} back out of the {displayName}; the other {_units} stayed in it, burner off.");
                        _state = CookerState.Loaded;
                        _flips = 0;
                        RefreshVisuals();
                    }
                    else
                    {
                        GameEvents.RaiseNotice($"Took the raw {_food.DisplayName} back out of the {displayName}.");
                        Reset();
                    }
                    break;
                }
                case CookerState.Cooking:
                    GameEvents.RaiseNotice($"Dumped {_units} half-cooked {_food.DisplayName} out of the {displayName}. Straight in the bin.");
                    Reset();
                    break;
                case CookerState.Done:
                case CookerState.Burning:
                    SendToCooler($"Sent {_units} cooked {_food.DisplayName} to the cooler.");
                    break;
            }
        }

        // ----- The steps -----

        private void Load(PlayerInventory inventory)
        {
            if (!inventory.IsHoldingRawFood || inventory.HeldFood.Cooker != kind) return;
            _food = inventory.HeldFood;
            _units = inventory.RemoveFoodUnits(inventory.HeldFoodUnits);
            _state = CookerState.Loaded;
            GameEvents.RaiseNotice(kind == CookerKind.Fryer
                ? $"{_units} raw {_food.DisplayName} in the basket. Hold E to lower it. (audio cue: frost hissing in the basket)"
                : $"{_units} raw {_food.DisplayName} in the {displayName}.");
            RefreshVisuals();
        }

        private void BeginFlipping()
        {
            _state = CookerState.Flipping;
            _flips = 0;
            _gaugePhase = 0f;
            GameEvents.RaiseNotice($"Burner lit. Flip the wok {FlipsNeeded} times: press E when the mark is in the hot zone. A bad flip throws a unit on the floor. (audio cue: a whoomp of gas)");
            RefreshVisuals();
        }

        private void AttemptFlip()
        {
            float marker = Marker();
            float window = config != null ? config.WokFlipWindow : 0.3f;
            if (Mathf.Abs(marker - 0.5f) <= window * 0.5f)
            {
                _flips++;
                _bounce = 1f;
                if (_flips >= FlipsNeeded)
                {
                    GameEvents.RaiseNotice("Clean flip. The wok is doing the rest. (audio cue: the sizzle that means it's working)");
                    StartCooking();
                }
                return;
            }

            _units = Mathf.Max(0, _units - 1);
            _bounce = 1f;
            if (_units <= 0)
            {
                GameEvents.RaiseNotice($"You flipped the last of the {_food.DisplayName} onto the floor. The wok is empty and so is your pride.");
                Reset();
                return;
            }
            GameEvents.RaiseNotice(marker < 0.5f
                ? $"Too early. A unit of {_food.DisplayName} went over the side. ({_units} left)"
                : $"Too late. A unit of {_food.DisplayName} went over the side. ({_units} left)");
            RefreshVisuals();
        }

        private void StartCooking()
        {
            _state = CookerState.Cooking;
            _batchSize = _units;
            _cookTotal = CookSeconds;
            _cookRemaining = _cookTotal;
            switch (kind)
            {
                case CookerKind.Fryer: GameEvents.RaiseNotice($"Basket down. {_units} {_food.DisplayName}, {Mathf.CeilToInt(_cookTotal)} seconds. (audio cue: the oil screaming)"); break;
                case CookerKind.Steamer: GameEvents.RaiseNotice($"Steamer set. {_units} {_food.DisplayName}, {Mathf.CeilToInt(_cookTotal)} seconds. One will stick to the basket; it's still a unit."); break;
                case CookerKind.RiceCooker: GameEvents.RaiseNotice($"COOK. The rice cooker clicks and does the only thing it knows. {Mathf.CeilToInt(_cookTotal)} seconds."); break;
                default: GameEvents.RaiseNotice($"{_units} {_food.DisplayName} on the wok for {Mathf.CeilToInt(_cookTotal)} seconds."); break;
            }
            RefreshVisuals();
        }

        private void FinishCooking()
        {
            _state = CookerState.Done;
            _doneSeconds = 0f;
            float grace = config != null ? config.BurnGraceSeconds : 30f;
            GameEvents.RaiseNotice($"{displayName}: {_units} {_food.DisplayName} done. Take it out (E) within {Mathf.RoundToInt(grace)}s or it starts to burn. (audio cue: a cheap ding)");
            RefreshVisuals();
        }

        private void TakeOut(PlayerInventory inventory)
        {
            if (!inventory.HandsFree)
            {
                GameEvents.RaiseNotice("Free your hands first.");
                return;
            }

            int taken = Mathf.Min(_units, inventory.FoodCapacity);
            if (!inventory.TryTakeFoodTray(_food, taken)) return;
            int leftover = _units - taken;
            string foodName = _food.DisplayName;
            FoodDefinition food = _food;
            Vector3 at = transform.position;
            Reset();
            if (leftover > 0)
            {
                GameEvents.RaiseCookedFoodStored(food, leftover, at);
                GameEvents.RaiseNotice($"Took {taken} cooked {foodName}; the other {leftover} went to the cooler.");
            }
            else
            {
                GameEvents.RaiseNotice($"Took {taken} cooked {foodName}. The buffet is that way.");
            }
        }

        private void SendToCooler(string notice)
        {
            FoodDefinition food = _food;
            int units = _units;
            Vector3 at = transform.position;
            Reset();
            if (food == null || units <= 0) return;
            GameEvents.RaiseCookedFoodStored(food, units, at);
            GameEvents.RaiseNotice(notice);
        }

        private void Reset()
        {
            _food = null;
            _units = 0;
            _state = CookerState.Empty;
            _flips = 0;
            _doneSeconds = 0f;
            _burnTimer = 0f;
            RefreshVisuals();
        }

        // ----- Time -----

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_bounce > 0f) _bounce = Mathf.Max(0f, _bounce - dt * 4f);

            switch (_state)
            {
                case CookerState.Flipping:
                    _gaugePhase += dt * GaugeSweepsPerSecond;
                    break;

                case CookerState.Cooking:
                    _cookRemaining -= dt * (SpeedActive ? _speedMultiplier : 1f);
                    if (_cookRemaining <= 0f) FinishCooking();
                    break;

                case CookerState.Done:
                    _doneSeconds += dt;
                    if (_doneSeconds >= (config != null ? config.BurnGraceSeconds : 30f)) BeginBurning();
                    break;

                case CookerState.Burning:
                    _burnTimer += dt;
                    float step = config != null ? Mathf.Max(1f, config.BurnStepSeconds) : 30f;
                    if (_burnTimer >= step)
                    {
                        _burnTimer -= step;
                        BurnStep();
                    }
                    break;
            }

            AnimateVisuals(dt);
        }

        private void BeginBurning()
        {
            _state = CookerState.Burning;
            _burnTimer = 0f;
            GameEvents.RaiseNotice($"The {_food.DisplayName} in the {displayName} is starting to burn. (audio cue: the sizzle turns to a crackle)");
            RefreshVisuals();
        }

        private void BurnStep()
        {
            float fraction = config != null ? config.BurnStepFraction : 0.25f;
            // A quarter of the whole batch each step (20 > 15 > 10 > 5 > nothing), not of what's left.
            int lost = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(_batchSize, _units) * fraction), 1, _units);
            _units -= lost;
            GameEvents.RaiseReputationNudged(-1f, "burning smell from the kitchen");
            if (_units <= 0)
            {
                GameEvents.RaiseNotice($"The {_food.DisplayName} in the {displayName} burned to nothing. The dining room can smell it.");
                Reset();
                return;
            }
            GameEvents.RaiseNotice($"{lost} {_food.DisplayName} burned in the {displayName}. {_units} left, and the dining room can smell it.");
            RefreshVisuals();
        }

        private void OnSpeedRequested(float multiplier, float seconds)
        {
            _speedMultiplier = Mathf.Max(0.05f, multiplier);
            _speedUntil = Time.time + Mathf.Max(0f, seconds);
        }

        /// <summary>
        /// At close whatever is still in the cooker gets put away; half-cooked food is finished off the
        /// clock. A wok whose burner was lit but never flipped hasn't started cooking, so its raw box
        /// stays in the wok (burner off) for the morning instead of going to the cooler as cooked stock.
        /// </summary>
        private void OnDayEnded(int day)
        {
            if (_food == null) return;
            switch (_state)
            {
                case CookerState.Flipping:
                    _state = CookerState.Loaded;
                    _flips = 0;
                    RefreshVisuals();
                    break;
                case CookerState.Cooking:
                case CookerState.Done:
                case CookerState.Burning:
                    SendToCooler($"Closing: {_units} {_food.DisplayName} from the {displayName} went into the cooler.");
                    break;
            }
        }

        private bool SpeedActive => Time.time < _speedUntil;
        private int FlipsNeeded => config != null ? Mathf.Max(1, config.WokFlipsNeeded) : 4;

        private float CookSeconds
        {
            get
            {
                if (config == null) return 20f;
                switch (kind)
                {
                    case CookerKind.Fryer: return config.FryerSeconds;
                    case CookerKind.Wok: return config.WokSeconds;
                    case CookerKind.Steamer: return config.SteamerSeconds;
                    default: return config.RiceCookerSeconds;
                }
            }
        }

        private float Marker() => Mathf.PingPong(_gaugePhase, 1f);

        private string Gauge()
        {
            float window = config != null ? config.WokFlipWindow : 0.3f;
            int markerIndex = Mathf.Clamp(Mathf.RoundToInt(Marker() * (GaugeSegments - 1)), 0, GaugeSegments - 1);
            var sb = new StringBuilder();
            for (int i = 0; i < GaugeSegments; i++)
            {
                float at = (i + 0.5f) / GaugeSegments;
                bool hot = Mathf.Abs(at - 0.5f) <= window * 0.5f;
                if (i == markerIndex) sb.Append("<color=#FFFFFF>O</color>");
                else sb.Append(hot ? "<color=#FFD24A>|</color>" : "<color=#666666>|</color>");
            }
            return sb.ToString();
        }

        // ----- Presentation -----

        private void RefreshVisuals()
        {
            if (label != null)
            {
                string line;
                switch (_state)
                {
                    case CookerState.Empty: line = "empty"; break;
                    case CookerState.Loaded: line = $"{_food.DisplayName} x{_units} (raw)"; break;
                    case CookerState.Flipping: line = $"flip {_flips}/{FlipsNeeded}"; break;
                    case CookerState.Cooking: line = $"{_food.DisplayName} cooking"; break;
                    case CookerState.Done: line = $"{_food.DisplayName} x{_units} DONE"; break;
                    default: line = $"{_food.DisplayName} x{_units} BURNING"; break;
                }
                label.text = $"{displayName.ToUpperInvariant()}\n{line}";
            }

            if (foodVisual != null)
            {
                bool show = _food != null && _units > 0;
                foodVisual.gameObject.SetActive(show);
                if (show)
                {
                    var renderer = foodVisual.GetComponent<Renderer>();
                    Color color = _state == CookerState.Burning ? Color.Lerp(_food.Color, Color.black, 0.6f)
                        : _state == CookerState.Loaded || _state == CookerState.Flipping ? Color.Lerp(_food.Color, new Color(0.85f, 0.9f, 0.95f), 0.5f)
                        : _food.Color;
                    if (renderer != null) renderer.sharedMaterial = MaterialLibrary.Get(color);
                    float fraction = Mathf.Clamp01(_units / 20f);
                    Vector3 scale = foodVisual.localScale;
                    foodVisual.localScale = new Vector3(scale.x, Mathf.Max(0.01f, _foodVisualHeight * fraction), scale.z);
                }
            }

            if (indicator != null)
            {
                Color color = _state == CookerState.Cooking || _state == CookerState.Flipping ? IndicatorOn
                    : _state == CookerState.Done ? IndicatorDone
                    : _state == CookerState.Burning ? IndicatorBurn
                    : IndicatorOff;
                indicator.sharedMaterial = MaterialLibrary.Get(color);
            }

            if (smokeVisual != null) smokeVisual.gameObject.SetActive(_state == CookerState.Burning);
        }

        private void AnimateVisuals(float dt)
        {
            if (basketVisual != null && kind == CookerKind.Fryer)
            {
                bool down = _state == CookerState.Cooking || _state == CookerState.Done || _state == CookerState.Burning;
                Vector3 target = down ? _basketUp - Vector3.up * _basketDrop : _basketUp;
                basketVisual.localPosition = Vector3.MoveTowards(basketVisual.localPosition, target, dt * 0.6f);
            }

            if (foodVisual != null)
            {
                // A flip: the food hops. Position only; the scale carries the unit count.
                foodVisual.localPosition = _foodBase + Vector3.up * (Mathf.Sin(_bounce * Mathf.PI) * 0.12f);
            }

            if (smokeVisual != null && smokeVisual.gameObject.activeSelf)
            {
                float s = 0.35f + 0.15f * Mathf.Sin(Time.time * 3f);
                smokeVisual.localScale = new Vector3(s, s * 1.4f, s);
            }

            if (indicator != null && _state == CookerState.Done)
            {
                // The done light blinks faster as the burn clock runs down.
                float grace = config != null ? config.BurnGraceSeconds : 30f;
                float rate = Mathf.Lerp(2f, 8f, Mathf.Clamp01(_doneSeconds / Mathf.Max(1f, grace)));
                indicator.sharedMaterial = MaterialLibrary.Get(Mathf.Repeat(Time.time * rate, 2f) < 1f ? IndicatorDone : IndicatorOff);
            }
        }
    }
}
