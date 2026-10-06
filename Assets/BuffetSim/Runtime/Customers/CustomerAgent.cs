using System;
using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Economy;
using BuffetSim.Tables;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Customers
{
    /// <summary>
    /// The customer loop from the design notes as a small state machine on a NavMeshAgent:
    /// walk in, wait in line, get a table, grab each wanted food from the buffet (deducting for
    /// anything missing), eat, pay at the register, leave.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CustomerAgent : MonoBehaviour
    {
        public enum State
        {
            WalkingToLine,
            WaitingInLine,
            Shopping,
            WalkingToTable,
            Eating,
            Paying,
            Leaving,
            Done,
        }

        private NavMeshAgent _agent;
        private CustomerContext _ctx;
        private CustomerOrder _order;
        private CustomerBill _bill;
        private DiningTable _table;
        private TextMesh _label;

        private State _state;
        private float _timer;
        private float _lineWait;
        private float _patience;
        private float _tableCheckTimer;
        private int _lineIndex = -1;
        private int _orderIndex = -1;
        private IFoodSource _currentSource;
        private bool _grabbing;

        public string CustomerName { get; private set; } = "Customer";
        public State CurrentState => _state;
        public CustomerOrder Order => _order;

        public event Action<CustomerAgent> Finished;

        public void Initialize(CustomerContext context, CustomerOrder order, string customerName, TextMesh label)
        {
            _ctx = context;
            _order = order;
            CustomerName = customerName;
            _label = label;
            _agent = GetComponent<NavMeshAgent>();

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
                case State.Leaving:
                    if (HasArrived()) EnterState(State.Done);
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
                case State.Leaving:
                    MoveTo(_ctx.ExitPoint);
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
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            EconomyConfig cfg = _ctx.Config;
            int plates = _ctx.Rng.Next(cfg.MinPlatesPerVisit, cfg.MaxPlatesPerVisit + 1);
            if (_table != null)
            {
                _table.Release(this, plates);
                _table = null;
            }
            EnterState(State.Paying);
        }

        private void Pay()
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
            EnterState(State.Leaving);
        }

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

        private void RefreshLabel()
        {
            if (_label == null) return;
            string status;
            switch (_state)
            {
                case State.WalkingToLine:
                case State.WaitingInLine:
                    status = _lineIndex == 0 ? "Waiting for a table" : $"In line (#{_lineIndex + 1})";
                    break;
                case State.Shopping:
                    status = _orderIndex >= 0 && _orderIndex < _order.Lines.Count
                        ? $"Grabbing {_order.Lines[_orderIndex].Units} {_order.Lines[_orderIndex].Food.DisplayName}"
                        : "Shopping";
                    break;
                case State.WalkingToTable:
                    status = "Going to table";
                    break;
                case State.Eating:
                    status = "Eating";
                    break;
                case State.Paying:
                    status = $"Paying ${_bill.Total:0.00}";
                    break;
                case State.Leaving:
                    status = "Leaving";
                    break;
                default:
                    status = string.Empty;
                    break;
            }
            _label.text = $"{CustomerName}\n{status}";
        }

        private void OnDestroy()
        {
            if (_ctx != null && _ctx.Queue != null) _ctx.Queue.Remove(this);
            if (_table != null) _table.Release(this, 0);
        }
    }
}
