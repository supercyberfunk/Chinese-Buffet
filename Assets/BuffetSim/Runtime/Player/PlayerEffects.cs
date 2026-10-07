using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// Timed things that happen to the player: fortunes, aliens, a fresh cigarette. Listens to
    /// <see cref="GameEvents.PlayerEffectRequested"/> and exposes the result as plain numbers the
    /// controller and interactor read (speed, can sprint, controls locked, camera tilt). Slips go
    /// through <see cref="TrySlip"/> so "you cannot slip" fortunes cover every spill in the building.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerEffects : MonoBehaviour
    {
        private const float StumbleSeconds = 0.7f;
        private const float AbductRiseSeconds = 3f;
        private const float AbductHeight = 9f;
        private const float MinAbductSeconds = 4f;

        private CharacterController _controller;
        private PlayerInventory _inventory;

        private float _sprintDisabledUntil = float.NegativeInfinity;
        private float _speedMultiplier = 1f;
        private float _speedUntil = float.NegativeInfinity;
        private float _sprintMultiplier = 1f;
        private float _sprintUntil = float.NegativeInfinity;
        private float _noSlipUntil = float.NegativeInfinity;
        private float _slipEveryInterval;
        private float _slipEveryUntil = float.NegativeInfinity;
        private float _slipEveryTimer;
        private int _plateCap;
        private float _plateCapUntil = float.NegativeInfinity;
        private int _foodCap;
        private float _foodCapUntil = float.NegativeInfinity;
        private int _appliedPlateCap;
        private int _appliedFoodCap;
        private float _stumbleUntil = float.NegativeInfinity;
        private float _knockedDownUntil = float.NegativeInfinity;

        private bool _abducted;
        private float _abductTimer;
        private float _abductTotal;
        private Vector3 _abductStart;
        private Vector3 _abductReturn;

        public bool SprintAllowed => Time.time >= _sprintDisabledUntil;
        public float SpeedMultiplier => Time.time < _speedUntil ? _speedMultiplier : 1f;
        public float SprintMultiplier => Time.time < _sprintUntil ? _sprintMultiplier : 1f;
        public bool CanSlip => Time.time >= _noSlipUntil && !_abducted;
        public bool IsAbducted => _abducted;
        public bool IsKnockedDown => Time.time < _knockedDownUntil;
        public bool IsStumbling => Time.time < _stumbleUntil;
        /// <summary>No walking, jumping or interacting (abducted, flat on the floor, mid-stumble). Looking around still works.</summary>
        public bool ControlsLocked => _abducted || IsKnockedDown || IsStumbling;

        /// <summary>Camera roll in degrees for the controller to add (falls and stumbles).</summary>
        public float ViewRoll
        {
            get
            {
                if (IsKnockedDown) return 70f;
                if (IsStumbling) return 18f * Mathf.Sin(Mathf.Clamp01((_stumbleUntil - Time.time) / StumbleSeconds) * Mathf.PI);
                return 0f;
            }
        }

        /// <summary>How far the camera drops in metres (falls and stumbles).</summary>
        public float ViewDip
        {
            get
            {
                if (IsKnockedDown) return 1.25f;
                if (IsStumbling) return 0.6f * Mathf.Sin(Mathf.Clamp01((_stumbleUntil - Time.time) / StumbleSeconds) * Mathf.PI);
                return 0f;
            }
        }

        /// <summary>One line for the HUD listing what is currently affecting you, or empty.</summary>
        public string StatusLine
        {
            get
            {
                float now = Time.time;
                var line = new System.Text.StringBuilder();
                if (_abducted) Append(line, $"ABDUCTED {Clock(_abductTotal - _abductTimer)}");
                if (IsKnockedDown) Append(line, $"Out cold {Clock(_knockedDownUntil - now)}");
                if (now < _sprintDisabledUntil) Append(line, $"No sprinting {Clock(_sprintDisabledUntil - now)}");
                if (now < _speedUntil && !Mathf.Approximately(_speedMultiplier, 1f)) Append(line, $"Speed x{_speedMultiplier:0.##} {Clock(_speedUntil - now)}");
                if (now < _sprintUntil && !Mathf.Approximately(_sprintMultiplier, 1f)) Append(line, $"Sprint x{_sprintMultiplier:0.##} {Clock(_sprintUntil - now)}");
                if (now < _noSlipUntil) Append(line, $"Can't slip {Clock(_noSlipUntil - now)}");
                if (now < _slipEveryUntil) Append(line, $"Slippery {Clock(_slipEveryUntil - now)}");
                if (now < _plateCapUntil) Append(line, $"Plate cap {_plateCap} {Clock(_plateCapUntil - now)}");
                if (now < _foodCapUntil) Append(line, $"Tray cap {_foodCap} {Clock(_foodCapUntil - now)}");
                return line.ToString();
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            EnsureRefs();
        }

        private void OnEnable()
        {
            GameEvents.PlayerEffectRequested += Apply;
        }

        private void OnDisable()
        {
            GameEvents.PlayerEffectRequested -= Apply;
        }

        /// <summary>Applies one effect. Durations of the same kind don't stack; the newest one wins (sprint bans extend).</summary>
        public void Apply(PlayerEffect effect)
        {
            EnsureRefs();
            float now = Time.time;
            float seconds = Mathf.Max(0f, effect.Seconds);
            switch (effect.Kind)
            {
                case PlayerEffectKind.SprintDisabled:
                    _sprintDisabledUntil = Mathf.Max(_sprintDisabledUntil, now + seconds);
                    break;
                case PlayerEffectKind.SpeedMultiplier:
                    _speedMultiplier = effect.Value > 0f ? effect.Value : 1f;
                    _speedUntil = now + seconds;
                    break;
                case PlayerEffectKind.SprintMultiplier:
                    _sprintMultiplier = effect.Value > 0f ? effect.Value : 1f;
                    _sprintUntil = now + seconds;
                    break;
                case PlayerEffectKind.NoSlip:
                    _noSlipUntil = Mathf.Max(_noSlipUntil, now + seconds);
                    break;
                case PlayerEffectKind.SlipEvery:
                    _slipEveryInterval = Mathf.Max(1f, effect.Value);
                    _slipEveryUntil = now + seconds;
                    _slipEveryTimer = _slipEveryInterval;
                    break;
                case PlayerEffectKind.PlateCapacity:
                    _plateCap = Mathf.Max(1, Mathf.RoundToInt(effect.Value));
                    _plateCapUntil = now + seconds;
                    ApplyCaps();
                    break;
                case PlayerEffectKind.FoodCapacity:
                    _foodCap = Mathf.Max(1, Mathf.RoundToInt(effect.Value));
                    _foodCapUntil = now + seconds;
                    ApplyCaps();
                    break;
                case PlayerEffectKind.DropEverything:
                    if (_inventory != null && !_inventory.SpillLoad(transform.position, string.IsNullOrEmpty(effect.Source) ? "Dropped everything" : effect.Source))
                        GameEvents.RaiseNotice("Your hands were already empty. The universe shrugs.");
                    break;
                case PlayerEffectKind.Abduct:
                    StartAbduction(Mathf.Max(MinAbductSeconds, seconds), effect.Position);
                    break;
                case PlayerEffectKind.KnockDown:
                    _knockedDownUntil = Mathf.Max(_knockedDownUntil, now + seconds);
                    if (_inventory != null) _inventory.SpillLoad(transform.position, "Knocked flat");
                    break;
                case PlayerEffectKind.Teleport:
                    Teleport(effect.Position);
                    break;
            }
        }

        /// <summary>
        /// A slip: a short stumble, and whatever you carry hits the floor (plates break, the pane
        /// shatters). Does nothing while a fortune keeps you on your feet or aliens have you. True if you went down.
        /// </summary>
        public bool TrySlip(string cause, bool announceEmptyHanded = false)
        {
            if (!CanSlip || IsKnockedDown) return false;
            EnsureRefs();
            _stumbleUntil = Time.time + StumbleSeconds;
            bool dropped = _inventory != null && _inventory.SpillLoad(transform.position, cause);
            if (!dropped && announceEmptyHanded) GameEvents.RaiseNotice($"{cause}. Nothing in your hands, so nothing broke.");
            return true;
        }

        /// <summary>Moves the player to <paramref name="position"/> (the controller has to be off for a direct move).</summary>
        public void Teleport(Vector3 position)
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (_controller != null) _controller.enabled = false;
            transform.position = position;
            if (_controller != null && wasEnabled) _controller.enabled = true;
        }

        private void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;

            if (_abducted)
            {
                TickAbduction(dt);
                return;
            }

            if (now < _slipEveryUntil)
            {
                _slipEveryTimer -= dt;
                if (_slipEveryTimer <= 0f)
                {
                    _slipEveryTimer = _slipEveryInterval;
                    TrySlip("Slipped on a perfectly dry floor", true);
                }
            }

            // A cap that ran out goes back to normal.
            int wantPlates = now < _plateCapUntil ? _plateCap : 0;
            int wantFood = now < _foodCapUntil ? _foodCap : 0;
            if (wantPlates != _appliedPlateCap || wantFood != _appliedFoodCap) ApplyCaps();
        }

        private void ApplyCaps()
        {
            EnsureRefs();
            if (_inventory == null) return;
            float now = Time.time;
            int plates = now < _plateCapUntil ? _plateCap : 0;
            int food = now < _foodCapUntil ? _foodCap : 0;
            _inventory.SetCapacityOverrides(plates, food);
            _appliedPlateCap = plates;
            _appliedFoodCap = food;
            if (plates <= 0 && food <= 0) return;

            // A lower cap than what you're holding: the extra falls off the top.
            int extraPlates = _inventory.HeldPlates - _inventory.PlateCapacity;
            if (extraPlates > 0)
            {
                _inventory.RemovePlates(extraPlates);
                GameEvents.RaiseNotice($"Your hands give out: {extraPlates} plate{(extraPlates == 1 ? "" : "s")} slid off the stack.");
                GameEvents.RaiseDishesBroken(extraPlates, transform.position);
            }
            int extraUnits = _inventory.HeldFoodUnits - _inventory.FoodCapacity;
            if (extraUnits > 0 && _inventory.IsHoldingFood)
            {
                string foodName = _inventory.HeldFood.DisplayName;
                _inventory.RemoveFoodUnits(extraUnits);
                GameEvents.RaiseNotice($"Your hands give out: {extraUnits} {foodName} slid off the tray.");
            }
        }

        private void StartAbduction(float seconds, Vector3 returnPosition)
        {
            if (_abducted) return;
            EnsureRefs();
            if (_inventory != null) _inventory.SpillLoad(transform.position, "Abducted");
            _abducted = true;
            _abductTimer = 0f;
            _abductTotal = seconds;
            _abductStart = transform.position;
            _abductReturn = returnPosition;
            _stumbleUntil = float.NegativeInfinity;
            if (_controller != null) _controller.enabled = false;
        }

        private void TickAbduction(float dt)
        {
            _abductTimer += dt;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_abductTimer / AbductRiseSeconds));
            float bob = Mathf.Sin(_abductTimer * 2.2f) * 0.25f * rise;
            transform.position = _abductStart + Vector3.up * (AbductHeight * rise + bob);
            transform.Rotate(0f, 25f * dt, 0f, Space.World);
            if (_abductTimer < _abductTotal) return;

            _abducted = false;
            Teleport(_abductReturn + Vector3.up * 0.1f);
            if (_controller != null) _controller.enabled = true;
            GameEvents.RaiseNotice("You were dropped off at the front door. You feel... examined.");
        }

        private void EnsureRefs()
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
        }

        private static void Append(System.Text.StringBuilder line, string part)
        {
            if (line.Length > 0) line.Append("   ");
            line.Append(part);
        }

        private static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
