using System;
using BuffetSim.Food;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// What the player is carrying: either a food tray (one food, some units) or a stack of dirty
    /// plates, never both. Stations read and mutate this through its public methods.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int plateCapacity = 4;

        public FoodDefinition HeldFood { get; private set; }
        public int HeldFoodUnits { get; private set; }
        public int HeldPlates { get; private set; }
        public int PlateCapacity => plateCapacity;

        public bool IsHoldingFood => HeldFood != null;
        public bool IsHoldingPlates => HeldPlates > 0;
        public bool HandsFree => !IsHoldingFood && !IsHoldingPlates;

        public event Action Changed;

        public void Configure(int plates)
        {
            plateCapacity = Mathf.Max(1, plates);
            Changed?.Invoke();
        }

        public bool TryTakeFoodTray(FoodDefinition food, int units)
        {
            if (!HandsFree || food == null || units <= 0) return false;
            HeldFood = food;
            HeldFoodUnits = units;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes up to <paramref name="count"/> units from the held tray; an emptied tray is put away.</summary>
        public int RemoveFoodUnits(int count)
        {
            int removed = Mathf.Clamp(count, 0, HeldFoodUnits);
            HeldFoodUnits -= removed;
            if (HeldFoodUnits <= 0)
            {
                HeldFood = null;
                HeldFoodUnits = 0;
            }
            Changed?.Invoke();
            return removed;
        }

        public int AddPlates(int count)
        {
            if (IsHoldingFood) return 0;
            int added = Mathf.Clamp(count, 0, plateCapacity - HeldPlates);
            HeldPlates += added;
            if (added > 0) Changed?.Invoke();
            return added;
        }

        public int RemovePlates(int count)
        {
            int removed = Mathf.Clamp(count, 0, HeldPlates);
            HeldPlates -= removed;
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        public void DropEverything()
        {
            HeldFood = null;
            HeldFoodUnits = 0;
            HeldPlates = 0;
            Changed?.Invoke();
        }

        public string Describe()
        {
            if (IsHoldingFood) return $"Carrying: {HeldFood.DisplayName} x{HeldFoodUnits}";
            if (IsHoldingPlates) return $"Carrying: {HeldPlates}/{plateCapacity} dirty plates";
            return "Hands free";
        }
    }
}
