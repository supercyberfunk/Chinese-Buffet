using System;
using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Economy;
using BuffetSim.Interaction;
using BuffetSim.Items;
using BuffetSim.Player;
using BuffetSim.Tables;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Customers
{
    /// <summary>
    /// The customer loop from the design notes as a small state machine on a NavMeshAgent:
    /// walk in, wait in line, get a table, grab each wanted food from the buffet (deducting for
    /// anything missing), eat, pay at the register, leave. Some customers bolt for the door instead
    /// of paying (dine and dash); the player can tackle them, which knocks them out and sprays their
    /// bill across the floor as coins. Chaos events, fortunes and thrown things reach a customer only
    /// through the public surface below: promote, restyle, slip, stagger, hold in place, pay now,
    /// storm out, vanish, follow someone, knock out, or hand the E key to the event for a while.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CustomerAgent : MonoBehaviour, IHoldInteractable, IThrowTarget
    {
        public enum State
        {
            WalkingToLine,
            WaitingInLine,
            Shopping,
            WalkingToTable,
            Eating,
            Paying,
            Dashing,
            Leaving,
            KnockedOut,
            Done,
        }

        private const float SlipSeconds = 2f;
        private const float KnockedOutLabelHeight = 0.9f;
        private const float FollowDistance = 1f;
        private const float FollowSpeed = 4.2f;
        private const float FollowRepathSeconds = 0.25f;
        private const float DoorTripDistance = 3.5f;
        private const float DoorTripSeconds = 5f;
        private static readonly Quaternion SlipTilt = Quaternion.Euler(28f, 0f, 20f);
        private static readonly Quaternion KnockedOutTilt = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Vector3 KnockedOutLift = new Vector3(0f, 0.32f, 0f);
        private static readonly Vector3 StandingColliderCenter = new Vector3(0f, 0.95f, 0f);
        private static readonly Vector3 LyingColliderCenter = new Vector3(0f, 0.35f, 0.95f);
        private static readonly Color AlertColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Color SuitColor = new Color(0.2f, 0.2f, 0.23f);

        private NavMeshAgent _agent;
        private CustomerContext _ctx;
        private CustomerOrder _order;
        private CustomerBill _bill;
        private DiningTable _table;
        private TextMesh _label;
        private CapsuleCollider _interactCollider;

        private State _state;
        private float _timer;
        private float _lineWait;
        private float _patience;
        private float _tableCheckTimer;
        private int _lineIndex = -1;
        private int _orderIndex = -1;
        private IFoodSource _currentSource;
        private bool _grabbing;

        // Dine and dash, knock-outs, slips and alerts.
        private float _baseSpeed;
        private bool _dashHesitating;
        private bool _dashResolved;
        private bool _escaped;
        private float _knockoutSeconds;
        private float _slipTimer;
        private bool _slipWasStopped;
        private bool _alert;
        private string _stumbleStatus = "Slipped!";
        private Color _labelBaseColor = Color.white;
        private Vector3 _labelRestPosition;
        private readonly List<Transform> _posedParts = new List<Transform>();
        private readonly List<Vector3> _posedPositions = new List<Vector3>();
        private readonly List<Quaternion> _posedRotations = new List<Quaternion>();

        // Event hooks: held in place, already paid, storming out, following someone, tripped at the door.
        private bool _held;
        private string _holdStatus;
        private bool _paid;
        private bool _stormingOut;
        private string _labelSuffix = string.Empty;
        private Transform _followTarget;
        private float _followTimer;
        private float _followRepath;
        private bool _returningToSeat;
        private bool _tripped;

        public string CustomerName { get; private set; } = "Customer";
        public State CurrentState => _state;
        public CustomerOrder Order => _order;

        /// <summary>Promoted by the "Business is booming" event: pays a multiplied bill.</summary>
        public bool IsSuit { get; private set; }
        public bool IsKnockedOut => _state == State.KnockedOut;
        public bool IsDashing => _state == State.Dashing;
        /// <summary>What the customer currently owes (base bill minus deductions; zero if nothing was served).</summary>
        public float BillTotal => _bill != null ? _bill.Total : 0f;
        /// <summary>The bill is settled (paid at the register, auto-paid by an event, or written off by storming out).</summary>
        public bool IsPaid => _paid;
        public bool IsHeld => _held;
        /// <summary>Sitting at their table eating (not following anyone around).</summary>
        public bool IsSeated => _state == State.Eating && _table != null && _followTarget == null && !_returningToSeat;
        public bool IsFollowing => _followTarget != null;
        /// <summary>The table this customer has reserved, or null.</summary>
        public DiningTable Table => _table;

        /// <summary>
        /// While set, the E key on this customer belongs to an event (calm the manager-demander, throw
        /// out the moderator). Holds and throws are forwarded too when the override implements them.
        /// </summary>
        public IInteractable InteractOverride { get; set; }

        public event Action<CustomerAgent> Finished;

        public void Initialize(CustomerContext context, CustomerOrder order, string customerName, TextMesh label)
        {
            _ctx = context;
            _order = order;
            CustomerName = customerName;
            _label = label;
            _agent = GetComponent<NavMeshAgent>();
            _baseSpeed = _agent.speed;
            if (_label != null)
            {
                _labelBaseColor = _label.color;
                _labelRestPosition = _label.transform.localPosition;
            }

            // The placeholder body's solid collider sits on the Ignore Raycast layer, so the player's
            // interaction ray needs a trigger on the root (Default layer) to find this agent.
            gameObject.layer = 0;
            _interactCollider = gameObject.AddComponent<CapsuleCollider>();
            _interactCollider.isTrigger = true;
            _interactCollider.height = 1.9f;
            _interactCollider.radius = 0.4f;
            _interactCollider.center = StandingColliderCenter;

            EconomyConfig cfg = _ctx.Config;
            // Base value is applied the moment the customer exists; it only ever goes down from here.
            _bill = new CustomerBill(cfg.BaseCustomerBill, order.TotalUnits, cfg.UnfulfilledUnitDeduction);
            _patience = cfg.LinePatienceSeconds * (float)(0.75 + _ctx.Rng.NextDouble() * 0.75);
            _tableCheckTimer = 0f;

            _lineIndex = _ctx.Queue.Enqueue(this);
            GameEvents.RaiseNotice($"{CustomerName} walked in wanting {order.Describe()}.");
            EnterState(State.WalkingToLine);
        }

        private void Update()
        {
            if (_ctx == null || _agent == null) return;

            if (_slipTimer > 0f)
            {
                // A stumble pauses whatever the customer was doing for a moment.
                _slipTimer -= Time.deltaTime;
                if (_slipTimer <= 0f) EndSlip();
                return;
            }

            // An event has them frozen in place (a dead lamp, a fallen tile, a complaint in progress).
            if (_held) return;

            switch (_state)
            {
                case State.WalkingToLine:
                case State.WaitingInLine:
                    TickLine();
                    break;
                case State.Shopping:
                    TickShopping();
                    break;
                case State.WalkingToTable:
                    if (HasArrived()) EnterState(State.Eating);
                    break;
                case State.Eating:
                    TickEating();
                    break;
                case State.Paying:
                    if (HasArrived()) Pay();
                    break;
                case State.Dashing:
                    TickDashing();
                    break;
                case State.Leaving:
                    if (HasArrived()) EnterState(State.Done);
                    break;
                case State.KnockedOut:
                    TickKnockedOut();
                    break;
            }
        }

        private void EnterState(State next)
        {
            _state = next;
            _timer = 0f;

            switch (next)
            {
                case State.WalkingToLine:
                    MoveTo(_ctx.Queue.SlotPosition(_lineIndex));
                    break;
                case State.Shopping:
                    _orderIndex = -1;
                    AdvanceOrder();
                    return; // AdvanceOrder refreshes the label itself (and may switch state again)
                case State.WalkingToTable:
                    MoveTo(_table.SeatPosition);
                    break;
                case State.Eating:
                    StopMoving();
                    if (_table != null) FaceTowards(_table.transform.position);
                    _timer = _ctx.Config.BaseEatSeconds + _ctx.Config.EatSecondsPerUnit * _bill.UnitsTaken;
                    break;
                case State.Paying:
                    MoveTo(_ctx.RegisterPoint);
                    break;
                case State.Dashing:
                    // Nervous pause at the table first ("!!"), then the sprint to the door.
                    StopMoving();
                    FaceTowards(_ctx.ExitPoint);
                    _dashHesitating = true;
                    _timer = _ctx.Config.DineAndDashHesitation;
                    GameEvents.RaiseNotice($"{CustomerName} is looking at the door... (audio cue: nervous whistle)");
                    GameEvents.RaiseDineAndDashStarted(CustomerName, transform.position);
                    break;
                case State.Leaving:
                    MoveTo(_ctx.ExitPoint);
                    break;
                case State.KnockedOut:
                    StopMoving();
                    _dashHesitating = false;
                    _timer = _knockoutSeconds;
                    ApplyPose(KnockedOutTilt, KnockedOutLift);
                    SetColliderLying(true);
                    if (_label != null)
                        _label.transform.localPosition = new Vector3(_labelRestPosition.x, KnockedOutLabelHeight, _labelRestPosition.z + 0.9f);
                    break;
                case State.Done:
                    Finished?.Invoke(this);
                    Destroy(gameObject);
                    return;
            }

            RefreshLabel();
        }

        private void TickLine()
        {
            _lineWait += Time.deltaTime;

            int index = _ctx.Queue.IndexOf(this);
            if (index >= 0 && index != _lineIndex)
            {
                _lineIndex = index;
                MoveTo(_ctx.Queue.SlotPosition(index));
                RefreshLabel();
            }

            if (_state == State.WalkingToLine && HasArrived())
                EnterState(State.WaitingInLine);

            if (_lineIndex == 0)
            {
                _tableCheckTimer -= Time.deltaTime;
                if (_tableCheckTimer <= 0f)
                {
                    _tableCheckTimer = _ctx.Config.TableCheckInterval;
                    if (_ctx.Tables.TryReserveTable(this, out _table))
                    {
                        _ctx.Queue.Remove(this);
                        EnterState(State.Shopping);
                        return;
                    }
                }
            }

            if (_lineWait > _patience)
            {
                _ctx.Queue.Remove(this);
                GameEvents.RaiseCustomerLost(CustomerName, transform.position);
                GameEvents.RaiseNotice($"{CustomerName} got fed up waiting in line and left.");
                EnterState(State.Leaving);
            }
        }

        private void AdvanceOrder()
        {
            _grabbing = false;
            _currentSource = null;

            while (true)
            {
                _orderIndex++;
                if (_orderIndex >= _order.Lines.Count)
                {
                    EnterState(State.WalkingToTable);
                    return;
                }

                CustomerOrder.Line line = _order.Lines[_orderIndex];
                _currentSource = _ctx.Buffet.FindSourceFor(line.Food);
                if (_currentSource == null)
                {
                    _bill.RecordServed(0, line.Units);
                    GameEvents.RaiseNotice($"{CustomerName} wanted {line.Units} {line.Food.DisplayName} but there is no tray for it (-${line.Units * _ctx.Config.UnfulfilledUnitDeduction:0.00}).");
                    continue;
                }

                MoveTo(_currentSource.StandPosition);
                RefreshLabel();
                return;
            }
        }

        private void TickShopping()
        {
            if (_currentSource == null)
            {
                AdvanceOrder();
                return;
            }

            if (!_grabbing)
            {
                if (!HasArrived()) return;
                _grabbing = true;
                _timer = _ctx.Config.GrabSeconds;
                if (_currentSource is Component sourceComponent) FaceTowards(sourceComponent.transform.position);
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            CustomerOrder.Line line = _order.Lines[_orderIndex];
            int taken = _currentSource.Take(line.Units);
            int missing = line.Units - taken;
            _bill.RecordServed(taken, missing);
            if (missing > 0)
                GameEvents.RaiseNotice($"{CustomerName} wanted {line.Units} {line.Food.DisplayName}, only got {taken} (-${missing * _ctx.Config.UnfulfilledUnitDeduction:0.00}).");

            AdvanceOrder();
        }

        private void TickEating()
        {
            if (_followTarget != null || _returningToSeat)
            {
                TickFollow();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            EconomyConfig cfg = _ctx.Config;
            int plates = _ctx.Rng.Next(cfg.MinPlatesPerVisit, cfg.MaxPlatesPerVisit + 1);
            if (_table != null)
            {
                _table.Release(this, plates);
                _table = null;
            }

            // Some customers eye the door instead of the register. Nothing owed means nothing to dash on.
            bool dashes = !_paid && _bill.Total > 0f && _ctx.Rng.NextDouble() < cfg.DineAndDashChance;
            EnterState(_paid ? State.Leaving : (dashes ? State.Dashing : State.Paying));
        }

        /// <summary>Trails the target at about a metre (not eating), then walks back to the seat and picks the fork back up.</summary>
        private void TickFollow()
        {
            if (_followTarget != null)
            {
                _followTimer -= Time.deltaTime;
                if (_followTimer <= 0f)
                {
                    StopFollowing();
                    return;
                }

                _followRepath -= Time.deltaTime;
                if (_followRepath > 0f) return;
                _followRepath = FollowRepathSeconds;
                Vector3 target = _followTarget.position;
                Vector3 away = transform.position - target;
                away.y = 0f;
                Vector3 spot = away.sqrMagnitude > 0.01f ? target + away.normalized * FollowDistance : target - _followTarget.forward * FollowDistance;
                MoveTo(spot);
                return;
            }

            if (!HasArrived()) return;
            _returningToSeat = false;
            StopMoving();
            if (_table != null) FaceTowards(_table.transform.position);
            RefreshLabel();
        }

        private void StopFollowing()
        {
            _followTarget = null;
            _followTimer = 0f;
            _agent.speed = _baseSpeed;
            if (_state == State.Eating && _table != null)
            {
                _returningToSeat = true;
                MoveTo(_table.SeatPosition);
            }
            RefreshLabel();
        }

        private void Pay()
        {
            // An event may have settled the bill on the way to the register already.
            if (!_paid)
            {
                _paid = true;
                RaiseReceipt();
            }
            EnterState(State.Leaving);
        }

        private void RaiseReceipt()
        {
            GameEvents.RaiseCustomerPaid(new CustomerReceipt
            {
                CustomerName = CustomerName,
                BaseAmount = _bill.BaseAmount,
                Deductions = _bill.Deductions,
                UnitsWanted = _bill.UnitsWanted,
                UnitsTaken = _bill.UnitsTaken,
                Total = _bill.Total,
                WorldPosition = transform.position,
            });
        }

        private void TickDashing()
        {
            if (_dashHesitating)
            {
                _timer -= Time.deltaTime;
                if (_timer > 0f) return;
                _dashHesitating = false;
                _agent.speed = _ctx.Config.DineAndDashSpeed;
                MoveTo(_ctx.ExitPoint);
                GameEvents.RaiseNotice($"{CustomerName} bolted for the door with ${_bill.Total:0.00} unpaid!");
                RefreshLabel();
                return;
            }

            // "What runs away comes back": while the fortune lasts, runners trip at the door and lie there.
            if (!_tripped && Time.time < _ctx.DashersTripUntil && ChaosHorizontalDistance(transform.position, _ctx.ExitPoint) < DoorTripDistance)
            {
                _tripped = true;
                GameEvents.RaiseNotice($"{CustomerName} tripped over the doormat. Get them!");
                BeginStumble(DoorTripSeconds, "Tripped at the door!");
                return;
            }

            if (!HasArrived()) return;

            // Out the door: the bill leaves with them. The ledger counts this as a lost customer.
            _escaped = true;
            _dashResolved = true;
            GameEvents.RaiseDineAndDashResolved(CustomerName, false, _bill.Total);
            EnterState(State.Done);
        }

        private void TickKnockedOut()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            GetUp();
        }

        private void GetUp()
        {
            RestorePose();
            SetColliderLying(false);
            if (_label != null) _label.transform.localPosition = _labelRestPosition;
            _agent.speed = _baseSpeed;
            GameEvents.RaiseNotice($"{CustomerName} came to and shuffled out.");
            EnterState(State.Leaving);
        }

        // ----- Public surface used by chaos events and the player -----

        /// <summary>
        /// "Business is booming": the bill is scaled, the body goes dark grey with a white tie and the
        /// label gets a " (suit)" suffix. No-op if already a suit or already on the way out.
        /// </summary>
        public void PromoteToSuit(float billMultiplier)
        {
            if (_ctx == null || IsSuit) return;
            if (_state == State.Leaving || _state == State.Done || _state == State.KnockedOut || _escaped) return;

            _bill.ApplyMultiplier(billMultiplier);
            IsSuit = true;

            Transform body = transform.Find("Placeholder/Body");
            if (body != null)
            {
                Renderer bodyRenderer = body.GetComponent<Renderer>();
                if (bodyRenderer != null) bodyRenderer.sharedMaterial = MaterialLibrary.Get(SuitColor);
                PrimitiveFactory.Visual("Tie", PrimitiveType.Cube, body.parent, new Vector3(0f, 1.3f, 0.36f), new Vector3(0.09f, 0.42f, 0.03f), MaterialLibrary.Get(Color.white));
            }
            RefreshLabel();
        }

        /// <summary>
        /// Stumble: stops for two seconds with the body tilted, then carries on with whatever it was
        /// doing. Returns false (and does nothing) if knocked out, already out the door, leaving or
        /// already slipping. The spill that caused it raises <see cref="GameEvents.CustomerSlipped"/>, not this.
        /// </summary>
        public bool Slip()
        {
            if (_ctx == null || _agent == null) return false;
            if (_slipTimer > 0f || _escaped) return false;
            if (_state == State.KnockedOut || _state == State.Leaving || _state == State.Done) return false;

            BeginStumble(SlipSeconds, "Slipped!");
            GameEvents.RaiseNotice($"{CustomerName} slipped and went down hard. (audio cue: wilhelm scream)");
            return true;
        }

        /// <summary>
        /// A short stun from something thrown or tripped over: tilted and frozen for <paramref name="seconds"/>,
        /// then back to whatever they were doing (runners keep running). Works in any state but out cold or gone.
        /// </summary>
        public bool Stagger(float seconds, string status = "Staggered")
        {
            if (_ctx == null || _agent == null || _escaped) return false;
            if (_state == State.KnockedOut || _state == State.Done) return false;
            if (_slipTimer > 0f)
            {
                _slipTimer = Mathf.Max(_slipTimer, seconds);
                return true;
            }
            BeginStumble(Mathf.Max(0.2f, seconds), status);
            return true;
        }

        /// <summary>
        /// Falls over for <paramref name="seconds"/>, releasing its table (no plates) and its spot in
        /// line, then gets up and leaves without paying. A dasher that goes down counts as caught:
        /// their bill (plus the bonus) sprays out as coins. No-op if already out cold or done.
        /// </summary>
        public void KnockOut(float seconds)
        {
            if (_ctx == null || _agent == null) return;
            if (_state == State.KnockedOut || _state == State.Done || _escaped) return;

            bool wasDashing = _state == State.Dashing;
            if (_slipTimer > 0f) EndSlip();
            _held = false;
            _holdStatus = null;
            _followTarget = null;
            _returningToSeat = false;

            if (_ctx.Queue != null) _ctx.Queue.Remove(this);
            if (_table != null)
            {
                _table.Release(this, 0);
                _table = null;
            }

            _knockoutSeconds = Mathf.Max(0.5f, seconds);
            EnterState(State.KnockedOut);

            if (wasDashing && !_dashResolved) ResolveDashCaught();
        }

        /// <summary>Red "!!" prefix on the label and a red label colour while on; other packages use it to mark targets.</summary>
        public void SetAlert(bool on)
        {
            if (_alert == on) return;
            _alert = on;
            RefreshLabel();
        }

        /// <summary>Frozen in place with <paramref name="status"/> on the label until <see cref="ReleaseHold"/> (events only). Line patience and the meal clock pause too.</summary>
        public void Hold(string status)
        {
            if (_ctx == null || _agent == null || _escaped) return;
            if (_state == State.KnockedOut || _state == State.Done) return;
            _held = true;
            _holdStatus = status;
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            RefreshLabel();
        }

        /// <summary>Changes the held label (countdowns) without releasing.</summary>
        public void SetHoldStatus(string status)
        {
            if (!_held || _holdStatus == status) return;
            _holdStatus = status;
            RefreshLabel();
        }

        public void ReleaseHold()
        {
            if (!_held) return;
            _held = false;
            _holdStatus = null;
            if (_state != State.KnockedOut && _state != State.Done) ResumeMovement();
            RefreshLabel();
        }

        /// <summary>
        /// The bill settles right now at whatever it stands (times <paramref name="multiplier"/>) and the
        /// table is freed with no plates on it. What happens to the customer next is up to the caller
        /// (knock out, vanish down a drain). False if already paid, gone or out the door.
        /// </summary>
        public bool PayNow(float multiplier = 1f)
        {
            if (_ctx == null || _paid || _escaped || _state == State.Done) return false;
            if (multiplier > 0f && !Mathf.Approximately(multiplier, 1f)) _bill.ApplyMultiplier(multiplier);
            _paid = true;
            if (_ctx.Queue != null) _ctx.Queue.Remove(this);
            if (_table != null)
            {
                _table.Release(this, 0);
                _table = null;
            }
            RaiseReceipt();
            RefreshLabel();
            return true;
        }

        /// <summary>
        /// Settles the bill as a coin spray instead of a receipt (the moderator pays double, in coins on
        /// the floor). Returns the amount sprayed; zero if already paid or nothing was owed.
        /// </summary>
        public float SettleAsCoins(float multiplier, string reason)
        {
            if (_ctx == null || _paid || _escaped || _state == State.Done) return 0f;
            float amount = _bill.Total * Mathf.Max(0f, multiplier);
            _paid = true;
            if (_ctx.Queue != null) _ctx.Queue.Remove(this);
            if (_table != null)
            {
                _table.Release(this, 0);
                _table = null;
            }
            if (amount > 0f) CoinPickup.Burst(transform.position, amount, _ctx.Config.CoinsPerBurst, reason);
            RefreshLabel();
            return amount;
        }

        /// <summary>Scales what they will pay (a fixed lamp is +10%, a heard complaint +25%).</summary>
        public void ApplyBillMultiplier(float multiplier)
        {
            if (_bill == null || _paid) return;
            _bill.ApplyMultiplier(multiplier);
            RefreshLabel();
        }

        /// <summary>
        /// Gets up and storms out without paying (counted as a walkout), from wherever they are. The
        /// table is freed with no plates. No-op if already leaving, out cold, paid or gone.
        /// </summary>
        public void LeaveWithoutPaying(string notice)
        {
            if (_ctx == null || _agent == null || _paid || _escaped) return;
            if (_state == State.Leaving || _state == State.Done || _state == State.KnockedOut) return;

            _paid = true;
            _stormingOut = true;
            _held = false;
            _holdStatus = null;
            _followTarget = null;
            _returningToSeat = false;
            if (_slipTimer > 0f) EndSlip();
            if (_ctx.Queue != null) _ctx.Queue.Remove(this);
            if (_table != null)
            {
                _table.Release(this, 0);
                _table = null;
            }
            GameEvents.RaiseCustomerLost(CustomerName, transform.position);
            if (!string.IsNullOrEmpty(notice)) GameEvents.RaiseNotice(notice);
            _agent.speed = _baseSpeed * 1.5f;
            EnterState(State.Leaving);
        }

        /// <summary>Gone, right now, no receipt and no walkout (dragged down a drain, beamed up). Frees the table and the line spot.</summary>
        public void Vanish()
        {
            if (_ctx == null || _state == State.Done) return;
            if (_slipTimer > 0f) EndSlip();
            EnterState(State.Done);
        }

        /// <summary>Line customers lose this share of their patience (someone in line is complaining loudly).</summary>
        public void LosePatience(float fraction)
        {
            if (_ctx == null) return;
            if (_state != State.WalkingToLine && _state != State.WaitingInLine) return;
            _lineWait += _patience * Mathf.Clamp01(fraction);
        }

        /// <summary>
        /// Gets up from the table and trails <paramref name="target"/> at about a metre for
        /// <paramref name="seconds"/>, not eating, then sits back down (the table stays theirs). Seated customers only.
        /// </summary>
        public bool Follow(Transform target, float seconds)
        {
            if (_ctx == null || target == null || !IsSeated || _held || _slipTimer > 0f) return false;
            _followTarget = target;
            _followTimer = Mathf.Max(1f, seconds);
            _followRepath = 0f;
            _agent.speed = FollowSpeed;
            RefreshLabel();
            return true;
        }

        public bool TryGetTablePosition(out Vector3 position)
        {
            if (_table == null)
            {
                position = default;
                return false;
            }
            position = _table.transform.position;
            return true;
        }

        /// <summary>New shirt colour and a label suffix (" (mod)"); null keeps the current suffix.</summary>
        public void Restyle(Color bodyColor, string labelSuffix)
        {
            Transform body = transform.Find("Placeholder/Body");
            if (body != null)
            {
                Renderer bodyRenderer = body.GetComponent<Renderer>();
                if (bodyRenderer != null) bodyRenderer.sharedMaterial = MaterialLibrary.Get(bodyColor);
            }
            if (labelSuffix != null) _labelSuffix = labelSuffix;
            RefreshLabel();
        }

        /// <summary>A primitive hung on the customer (hats, trench coats, fedoras); it falls over with them.</summary>
        public GameObject AddAccessory(string accessoryName, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return PrimitiveFactory.Visual(accessoryName, type, transform, localPosition, localScale, MaterialLibrary.Get(color));
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_ctx == null) return string.Empty;
            IInteractable custom = ActiveOverride;
            if (custom != null) return custom.GetPrompt(inventory);
            if (IsDashing) return $"[E] Tackle {CustomerName}!";
            if (IsKnockedOut) return $"{CustomerName} is out cold";
            string status = StatusText();
            return string.IsNullOrEmpty(status) ? CustomerName : $"{CustomerName}: {status}";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_ctx == null) return;
            IInteractable custom = ActiveOverride;
            if (custom != null)
            {
                custom.Interact(inventory);
                return;
            }
            if (!IsDashing) return;
            KnockOut(_ctx.Config.KnockoutSeconds);
        }

        public float GetHoldSeconds(PlayerInventory inventory)
        {
            return ActiveOverride is IHoldInteractable hold ? hold.GetHoldSeconds(inventory) : 0f;
        }

        public void CompleteHold(PlayerInventory inventory)
        {
            if (ActiveOverride is IHoldInteractable hold) hold.CompleteHold(inventory);
        }

        // ----- Thrown things -----

        public bool AcceptsThrow(ThrowableKind kind)
        {
            if (ActiveOverride is IThrowTarget custom) return custom.AcceptsThrow(kind);
            if (_ctx == null || _escaped || _state == State.Done || _state == State.KnockedOut) return false;
            // The rock only wants someone worth hitting: a runner or a diner.
            if (kind == ThrowableKind.Rock) return IsDashing || IsSeated;
            return true;
        }

        public int HomingPriority
        {
            get
            {
                if (ActiveOverride is IThrowTarget custom) return custom.HomingPriority;
                if (IsDashing) return 2;
                return IsSeated ? 1 : 0;
            }
        }

        public Vector3 AimPoint => transform.position + Vector3.up * (_state == State.KnockedOut ? 0.4f : 1.3f);

        public void OnThrowHit(ThrowableKind kind)
        {
            if (ActiveOverride is IThrowTarget custom)
            {
                custom.OnThrowHit(kind);
                return;
            }
            if (_ctx == null) return;

            switch (kind)
            {
                case ThrowableKind.Rock:
                    if (IsDashing)
                    {
                        GameEvents.RaiseNotice($"The rock found {CustomerName} mid-sprint.");
                        KnockOut(_ctx.Config.KnockoutSeconds);
                    }
                    else if (IsSeated)
                    {
                        PayNow();
                        GameEvents.RaiseNotice($"A rock out of nowhere: {CustomerName} is out cold. The bill paid itself and the table is clear.");
                        KnockOut(_ctx.Config.KnockoutSeconds);
                    }
                    break;
                case ThrowableKind.Dodgeball:
                    if (Stagger(1f, "Bonk!")) GameEvents.RaiseNotice($"Bonk. {CustomerName} is fine. Probably.");
                    break;
            }
        }

        /// <summary>The override, unless the event that set it has since been destroyed.</summary>
        private IInteractable ActiveOverride
        {
            get
            {
                IInteractable custom = InteractOverride;
                if (custom is UnityEngine.Object unityObject && unityObject == null)
                {
                    InteractOverride = null;
                    return null;
                }
                return custom;
            }
        }

        private void ResumeMovement()
        {
            switch (_state)
            {
                case State.WalkingToLine:
                case State.WaitingInLine:
                    MoveTo(_ctx.Queue.SlotPosition(Mathf.Max(0, _lineIndex)));
                    break;
                case State.Shopping:
                    if (_currentSource != null && !_grabbing) MoveTo(_currentSource.StandPosition);
                    break;
                case State.WalkingToTable:
                    if (_table != null) MoveTo(_table.SeatPosition);
                    break;
                case State.Eating:
                    if (_returningToSeat && _table != null) MoveTo(_table.SeatPosition);
                    break;
                case State.Paying:
                    MoveTo(_ctx.RegisterPoint);
                    break;
                case State.Dashing:
                    if (!_dashHesitating) MoveTo(_ctx.ExitPoint);
                    break;
                case State.Leaving:
                    MoveTo(_ctx.ExitPoint);
                    break;
            }
        }

        private void ResolveDashCaught()
        {
            EconomyConfig cfg = _ctx.Config;
            float amount = _bill.Total * (1f + cfg.DineAndDashBonus);
            _dashResolved = true;
            CoinPickup.Burst(transform.position, amount, cfg.CoinsPerBurst, $"{CustomerName}'s bill");
            GameEvents.RaiseDineAndDashResolved(CustomerName, true, amount);
            GameEvents.RaiseNotice($"You tackled {CustomerName}. Their wallet exploded.");
        }

        private void BeginStumble(float seconds, string status)
        {
            _slipTimer = seconds;
            _stumbleStatus = status;
            _slipWasStopped = _agent.isOnNavMesh && _agent.isStopped;
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            ApplyPose(SlipTilt, Vector3.zero);
            RefreshLabel();
        }

        private void EndSlip()
        {
            _slipTimer = 0f;
            RestorePose();
            if (_agent.isOnNavMesh && _state != State.KnockedOut) _agent.isStopped = _slipWasStopped || _held;
            RefreshLabel();
        }

        private static float ChaosHorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ----- Body posing (placeholder and GLB alike: every child except the label) -----

        /// <summary>Rotates every visual child around the feet and offsets it; the previous pose is restored first.</summary>
        private void ApplyPose(Quaternion rotation, Vector3 offset)
        {
            RestorePose();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform part = transform.GetChild(i);
                if (_label != null && part == _label.transform) continue;
                _posedParts.Add(part);
                _posedPositions.Add(part.localPosition);
                _posedRotations.Add(part.localRotation);
                part.localPosition = rotation * part.localPosition + offset;
                part.localRotation = rotation * part.localRotation;
            }
        }

        private void RestorePose()
        {
            for (int i = 0; i < _posedParts.Count; i++)
            {
                Transform part = _posedParts[i];
                if (part == null) continue;
                part.localPosition = _posedPositions[i];
                part.localRotation = _posedRotations[i];
            }
            _posedParts.Clear();
            _posedPositions.Clear();
            _posedRotations.Clear();
        }

        private void SetColliderLying(bool lying)
        {
            if (_interactCollider == null) return;
            _interactCollider.direction = lying ? 2 : 1; // 2 = Z axis, 1 = Y axis
            _interactCollider.center = lying ? LyingColliderCenter : StandingColliderCenter;
        }

        // ----- Movement helpers -----

        private void MoveTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    _agent.Warp(hit.position);
                else
                    return;
            }
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        private void StopMoving()
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        private bool HasArrived()
        {
            if (!_agent.isOnNavMesh) return true;
            if (_agent.pathPending) return false;
            if (_agent.remainingDistance > _agent.stoppingDistance + 0.2f) return false;
            return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.05f;
        }

        private void FaceTowards(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
        }

        // ----- Label -----

        private string StatusText()
        {
            if (_slipTimer > 0f) return _stumbleStatus;
            if (_held && !string.IsNullOrEmpty(_holdStatus)) return _holdStatus;
            if (_followTarget != null) return "Following you. Not eating.";
            switch (_state)
            {
                case State.WalkingToLine:
                case State.WaitingInLine:
                    return _lineIndex == 0 ? "Waiting for a table" : $"In line (#{_lineIndex + 1})";
                case State.Shopping:
                    return _orderIndex >= 0 && _orderIndex < _order.Lines.Count
                        ? $"Grabbing {_order.Lines[_orderIndex].Units} {_order.Lines[_orderIndex].Food.DisplayName}"
                        : "Shopping";
                case State.WalkingToTable:
                    return "Going to table";
                case State.Eating:
                    return _returningToSeat ? "Going back to the table" : "Eating";
                case State.Paying:
                    return $"Paying ${_bill.Total:0.00}";
                case State.Dashing:
                    return _dashHesitating ? "!!" : "Running for it!";
                case State.Leaving:
                    return _stormingOut ? "Storming out" : "Leaving";
                case State.KnockedOut:
                    return "Out cold";
                default:
                    return string.Empty;
            }
        }

        private void RefreshLabel()
        {
            if (_label == null) return;
            string prefix = _alert ? "!! " : string.Empty;
            string suffix = (IsSuit ? " (suit)" : string.Empty) + _labelSuffix;
            _label.text = $"{prefix}{CustomerName}{suffix}\n{StatusText()}";
            _label.color = _alert || _state == State.Dashing ? AlertColor : _labelBaseColor;
        }

        private void OnDestroy()
        {
            // No events here: a customer destroyed mid-visit (or mid-dash) just frees what it held.
            if (_ctx != null && _ctx.Queue != null) _ctx.Queue.Remove(this);
            if (_table != null) _table.Release(this, 0);
        }
    }
}
