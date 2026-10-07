using System;
using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A part of the building that an event can break and the player can fix with the right item
    /// held for a while: the front window (glass pane) and the wall the pitcher boy comes through
    /// (duct tape). Owns the intact and broken looks and the <see cref="RepairPoint"/>. Still broken
    /// when the doors close: the landlord fixes it and bills the till.
    /// </summary>
    public sealed class BreakablePanel : MonoBehaviour
    {
        private string _job;
        private string _itemId;
        private string _itemName;
        private string _where;
        private float _holdSeconds;
        private GameObject _intact;
        private GameObject _broken;
        private Vector3 _repairPosition;
        private Vector3 _triggerSize;
        private float _closeFee;
        private string _closeNotice;
        private RepairPoint _repairPoint;
        private bool _isBroken;

        public bool IsBroken => _isBroken;
        public Vector3 Position => _repairPosition;

        /// <summary>Raised when the player (not the landlord) fixes it.</summary>
        public event Action Repaired;

        public void Configure(string job, string itemId, string itemName, string whereToGetIt, float holdSeconds, GameObject intactVisual, GameObject brokenVisual,
            Vector3 repairPosition, Vector3 triggerSize, float closeFee, string closeNotice)
        {
            _job = job;
            _itemId = itemId;
            _itemName = itemName;
            _where = whereToGetIt;
            _holdSeconds = holdSeconds;
            _intact = intactVisual;
            _broken = brokenVisual;
            _repairPosition = repairPosition;
            _triggerSize = triggerSize;
            _closeFee = closeFee;
            _closeNotice = closeNotice;
            if (_broken != null) _broken.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            GameEvents.DayEnded -= OnDayEnded;
        }

        /// <summary>Breaks it (no-op if already broken) and puts up the repair job.</summary>
        public void Break()
        {
            if (_isBroken) return;
            _isBroken = true;
            if (_intact != null) _intact.SetActive(false);
            if (_broken != null) _broken.SetActive(true);
            _repairPoint = RepairPoint.Create("Repair - " + _job, transform, _repairPosition, _triggerSize, _itemId, _itemName, _holdSeconds, _job, _where, OnPlayerRepaired);
        }

        public void Repair()
        {
            if (!_isBroken) return;
            _isBroken = false;
            if (_intact != null) _intact.SetActive(true);
            if (_broken != null) _broken.SetActive(false);
            if (_repairPoint != null)
            {
                Destroy(_repairPoint.gameObject);
                _repairPoint = null;
            }
        }

        private void OnPlayerRepaired()
        {
            Repair();
            GameEvents.RaiseNotice($"{_job}: done.");
            Repaired?.Invoke();
        }

        private void OnDayEnded(int day)
        {
            if (!_isBroken) return;
            if (_closeFee > 0f) GameEvents.RaiseExpenseCharged(_closeFee, _closeNotice ?? $"The landlord fixed it: {_job}", _repairPosition);
            Repair();
        }
    }
}
