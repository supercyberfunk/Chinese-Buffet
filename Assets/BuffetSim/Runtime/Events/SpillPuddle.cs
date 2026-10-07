using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A puddle of sweet and sour on the dining floor. Three presses of the mop (E) clear it. Anyone
    /// who walks through it goes down: the player drops whatever they carry (plates break), customers
    /// stumble and the store pays them hush money through the event bus.
    /// </summary>
    public sealed class SpillPuddle : MonoBehaviour, IInteractable
    {
        private const int PressesToMop = 3;
        private const float SlipRadius = 0.6f;
        private const float PlayerSlipCooldown = 3f;
        private const float CustomerCheckInterval = 0.5f;

        private readonly HashSet<CustomerAgent> _tripped = new HashSet<CustomerAgent>();
        private SpillRunner _owner;
        private ChaosEventContext _ctx;
        private Transform _visual;
        private Vector3 _fullScale = Vector3.one;
        private PlayerInventory _playerInventory;
        private PlayerEffects _playerEffects;
        private int _pressesLeft = PressesToMop;
        private float _playerCooldown;
        private float _customerTimer;
        private bool _mopped;

        public int PressesLeft => _pressesLeft;
        public bool IsMopped => _mopped;

        public void Initialize(SpillRunner owner, ChaosEventContext ctx, Transform visual)
        {
            _owner = owner;
            _ctx = ctx;
            _visual = visual;
            if (visual != null) _fullScale = visual.localScale;
            _playerInventory = ctx != null && ctx.Player != null ? ctx.Player.GetComponent<PlayerInventory>() : null;
            _playerEffects = ctx != null && ctx.Player != null ? ctx.Player.GetComponent<PlayerEffects>() : null;
            // Stagger the customer checks so several puddles don't all scan on the same frame.
            _customerTimer = ctx != null ? (float)ctx.Rng.NextDouble() * CustomerCheckInterval : 0f;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            return _mopped ? string.Empty : $"[E] Mop ({_pressesLeft} more)";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_mopped) return;
            _pressesLeft--;
            if (_pressesLeft > 0)
            {
                if (_visual != null)
                {
                    float f = Mathf.Lerp(0.3f, 1f, (float)_pressesLeft / PressesToMop);
                    _visual.localScale = new Vector3(_fullScale.x * f, _fullScale.y, _fullScale.z * f);
                }
                return;
            }

            _mopped = true;
            GameEvents.RaiseNotice("Mopped.");
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_mopped || _ctx == null) return;
            float dt = Time.deltaTime;

            if (_playerCooldown > 0f)
                _playerCooldown -= dt;
            else if (_playerInventory != null && _ctx.Player != null && !_playerInventory.HandsFree
                     && ChaosActors.HorizontalDistance(_ctx.Player.position, transform.position) <= SlipRadius)
                SlipPlayer();

            _customerTimer -= dt;
            if (_customerTimer <= 0f)
            {
                _customerTimer = CustomerCheckInterval;
                CheckCustomers();
            }
        }

        private void SlipPlayer()
        {
            _playerCooldown = PlayerSlipCooldown;
            // Effects decide whether you can slip at all (a fortune can keep you upright) and what breaks.
            if (_playerEffects != null)
            {
                _playerEffects.TrySlip("Slipped on the spill");
                return;
            }
            _playerInventory.SpillLoad(_ctx.Player.position, "Slipped on the spill");
        }

        /// <summary>Each customer who walks through goes down once per puddle; the spill, not the customer, publishes the slip.</summary>
        private void CheckCustomers()
        {
            IReadOnlyList<CustomerAgent> customers = _ctx.GetCustomersInStore();
            for (int i = 0; i < customers.Count; i++)
            {
                CustomerAgent customer = customers[i];
                if (customer == null || _tripped.Contains(customer)) continue;
                if (ChaosActors.HorizontalDistance(customer.transform.position, transform.position) > SlipRadius) continue;
                if (!customer.Slip()) continue;
                _tripped.Add(customer);
                GameEvents.RaiseCustomerSlipped(customer.CustomerName, customer.transform.position);
            }
        }

        private void OnDestroy()
        {
            // Scene teardown is not a mopped floor: don't let the runner resolve the event on the way out.
            if (!gameObject.scene.isLoaded) return;
            if (_owner != null) _owner.OnPuddleGone(this);
        }
    }
}
