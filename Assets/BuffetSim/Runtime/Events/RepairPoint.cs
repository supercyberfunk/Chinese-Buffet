using System;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A job that needs the right thing in your hands and E held for a while: a hole in the wall
    /// (duct tape), a broken window (glass pane), a dark lamp (lightbulb), a burst pipe (wrench).
    /// Events spawn one, tell it what it needs, and get a callback when the bar fills. The prompt
    /// says what is missing; a wrong tool says "Wrong tool" and nothing else.
    /// </summary>
    public sealed class RepairPoint : MonoBehaviour, IHoldInteractable
    {
        private string _itemId;
        private string _itemName;
        private string _job;
        private string _where;
        private float _holdSeconds;
        private Action _onRepaired;
        private bool _repaired;

        public bool IsRepaired => _repaired;

        /// <summary>
        /// An empty object with a trigger box the player's interaction ray can find, placed at
        /// <paramref name="position"/>, configured for one job. The caller adds its own visuals.
        /// </summary>
        public static RepairPoint Create(string name, Transform parent, Vector3 position, Vector3 triggerSize, string itemId, string itemName,
            float holdSeconds, string job, string whereToGetIt, Action onRepaired)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = 0;

            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;

            RepairPoint point = go.AddComponent<RepairPoint>();
            point._itemId = itemId;
            point._itemName = itemName;
            point._job = job;
            point._where = whereToGetIt;
            point._holdSeconds = Mathf.Max(0.5f, holdSeconds);
            point._onRepaired = onRepaired;
            return point;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_repaired) return string.Empty;
            if (inventory == null) return _job;
            if (inventory.IsHolding(_itemId)) return $"[Hold E] {_job}";
            if (inventory.IsHoldingItem) return $"{_job}: wrong tool";
            return string.IsNullOrEmpty(_where) ? $"{_job}: needs {_itemName}" : $"{_job}: needs {_itemName} ({_where})";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_repaired || inventory == null || inventory.IsHolding(_itemId)) return;
            GameEvents.RaiseNotice(string.IsNullOrEmpty(_where)
                ? $"You need {_itemName} for that."
                : $"You need {_itemName} for that. Try {_where}.");
        }

        public float GetHoldSeconds(PlayerInventory inventory)
        {
            return !_repaired && inventory != null && inventory.IsHolding(_itemId) ? _holdSeconds : 0f;
        }

        public void CompleteHold(PlayerInventory inventory)
        {
            if (_repaired || inventory == null || !inventory.UseItem(_itemId)) return;
            _repaired = true;
            Action callback = _onRepaired;
            _onRepaired = null;
            callback?.Invoke();
        }
    }
}
