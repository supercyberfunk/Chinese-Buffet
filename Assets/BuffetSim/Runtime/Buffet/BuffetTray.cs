using System;
using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Buffet
{
    /// <summary>
    /// One tray on the buffet line: a single food, up to 20 units. Customers take from it,
    /// the player refills it from a carried food tray. Self-contained; publishes <see cref="Changed"/>.
    /// </summary>
    public sealed class BuffetTray : MonoBehaviour, IFoodSource, IInteractable
    {
        [SerializeField] private FoodDefinition food;
        [SerializeField] private int capacity = 20;
        [SerializeField] private int units;
        [SerializeField] private int lowWarningThreshold = 5;
        [SerializeField] private Transform standPoint;

        [Header("Visuals (optional)")]
        [SerializeField] private Transform fillVisual;
        [SerializeField] private TextMesh label;
        [SerializeField] private GameObject lowWarningVisual;

        private float _fullFillHeight = 0.3f;
        private float _fillBaseY;

        public FoodDefinition Food => food;
        public int Units => units;
        public int Capacity => capacity;
        public bool IsEmpty => units <= 0;
        public bool IsFull => units >= capacity;
        public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position - transform.forward;

        public event Action<BuffetTray> Changed;

        public void Configure(FoodDefinition trayFood, int trayCapacity, int initialUnits, int lowThreshold, Transform stand)
        {
            food = trayFood;
            capacity = Mathf.Max(1, trayCapacity);
            units = Mathf.Clamp(initialUnits, 0, capacity);
            lowWarningThreshold = lowThreshold;
            standPoint = stand;
            RefreshVisuals();
        }

        public void SetVisuals(Transform fill, float fullHeight, float baseY, TextMesh trayLabel, GameObject warning)
        {
            fillVisual = fill;
            _fullFillHeight = fullHeight;
            _fillBaseY = baseY;
            label = trayLabel;
            lowWarningVisual = warning;
            RefreshVisuals();
        }

        public int Take(int requested)
        {
            int taken = Mathf.Clamp(requested, 0, units);
            if (taken == 0) return 0;
            units -= taken;
            RefreshVisuals();
            Changed?.Invoke(this);
            return taken;
        }

        public int Refill(int offered)
        {
            int accepted = Mathf.Clamp(offered, 0, capacity - units);
            if (accepted == 0) return 0;
            units += accepted;
            RefreshVisuals();
            Changed?.Invoke(this);
            return accepted;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            string foodName = food != null ? food.DisplayName : "Empty";
            string state = $"{foodName} tray: {units}/{capacity}";
            if (inventory == null) return state;

            if (inventory.IsHoldingFood && inventory.HeldFood == food)
            {
                return IsFull
                    ? $"{state} (full)"
                    : $"[E] Refill {state} with your {inventory.HeldFoodUnits} {foodName}";
            }

            if (inventory.IsHoldingFood)
                return $"{state} (you're holding {inventory.HeldFood.DisplayName})";

            return $"{state} - bring a tray of {foodName} from the cooler";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.IsHoldingFood || inventory.HeldFood != food) return;
            if (IsFull)
            {
                GameEvents.RaiseNotice($"{food.DisplayName} tray is already full.");
                return;
            }

            // One click fills the buffet tray up to its maximum; whatever doesn't fit stays in hand.
            int space = capacity - units;
            int moved = inventory.RemoveFoodUnits(space);
            Refill(moved);
            GameEvents.RaiseNotice($"Refilled {food.DisplayName} with {moved} units ({units}/{capacity}).");
        }

        private void RefreshVisuals()
        {
            float fraction = capacity > 0 ? (float)units / capacity : 0f;

            if (fillVisual != null)
            {
                float height = Mathf.Max(0.005f, fraction * _fullFillHeight);
                Vector3 scale = fillVisual.localScale;
                fillVisual.localScale = new Vector3(scale.x, height, scale.z);
                Vector3 pos = fillVisual.localPosition;
                fillVisual.localPosition = new Vector3(pos.x, _fillBaseY + height * 0.5f, pos.z);
                fillVisual.gameObject.SetActive(units > 0);
            }

            if (label != null)
                label.text = food != null ? $"{food.DisplayName}\n{units}/{capacity}" : $"{units}/{capacity}";

            if (lowWarningVisual != null)
                lowWarningVisual.SetActive(units < lowWarningThreshold);
        }
    }
}
