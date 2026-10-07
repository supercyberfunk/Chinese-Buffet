using System;
using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Food;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>Ids of the two-handed things the player can carry besides food and plates.</summary>
    public static class CarryItems
    {
        public const string Lightbulb = "lightbulb";
        public const string DuctTape = "duct-tape";
        public const string Wrench = "wrench";
        public const string GlassPane = "glass-pane";
        public const string Tarp = "tarp";
        public const string LoadedPlate = "loaded-plate";
    }

    /// <summary>
    /// What the player has in their hands. Exactly one kind of load at a time: a food tray (cooked,
    /// or a raw box for the cookers), a stack of dirty plates, a to-go box being packed, or one
    /// two-handed item (a lightbulb, the duct tape, the glass pane). Stations read and change it
    /// through the methods below; <see cref="SpillLoad"/> is the one place that decides what breaks.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public const int DefaultFoodCapacity = 20;

        [SerializeField] private int plateCapacity = 4;

        private readonly Dictionary<FoodDefinition, int> _toGo = new Dictionary<FoodDefinition, int>();
        private readonly List<FoodDefinition> _toGoOrder = new List<FoodDefinition>();
        private int _plateCapacityOverride = -1;
        private int _foodCapacityOverride = -1;

        public FoodDefinition HeldFood { get; private set; }
        public int HeldFoodUnits { get; private set; }
        /// <summary>True when the held food is an uncooked box for the fryer, wok, steamer or rice cooker.</summary>
        public bool HeldFoodRaw { get; private set; }
        public int HeldPlates { get; private set; }

        public string HeldItemId { get; private set; }
        public string HeldItemName { get; private set; }
        /// <summary>Uses left on the held item (duct tape strips); 1 for single-use items.</summary>
        public int HeldItemUses { get; private set; }
        public bool HeldItemBlocksSprint { get; private set; }
        /// <summary>Breaks when dropped (lightbulb, glass pane).</summary>
        public bool HeldItemFragile { get; private set; }

        public bool IsHoldingToGoBox { get; private set; }
        public int ToGoUnits { get; private set; }

        /// <summary>Plates you can carry right now (fortunes can raise or lower it for a while).</summary>
        public int PlateCapacity => _plateCapacityOverride > 0 ? _plateCapacityOverride : plateCapacity;
        /// <summary>Units a carried tray can hold right now.</summary>
        public int FoodCapacity => _foodCapacityOverride > 0 ? _foodCapacityOverride : DefaultFoodCapacity;

        public bool IsHoldingFood => HeldFood != null;
        public bool IsHoldingCookedFood => HeldFood != null && !HeldFoodRaw;
        public bool IsHoldingRawFood => HeldFood != null && HeldFoodRaw;
        public bool IsHoldingPlates => HeldPlates > 0;
        public bool IsHoldingItem => !string.IsNullOrEmpty(HeldItemId);
        /// <summary>Anything other than plates: a tray, a box, an item or a to-go box.</summary>
        public bool HasNonPlateLoad => IsHoldingFood || IsHoldingItem || IsHoldingToGoBox;
        public bool HandsFree => !IsHoldingFood && !IsHoldingPlates && !IsHoldingItem && !IsHoldingToGoBox;

        /// <summary>The foods in the to-go box in packing order; <see cref="CountInToGoBox"/> has the units.</summary>
        public IReadOnlyList<FoodDefinition> ToGoFoods => _toGoOrder;

        /// <summary>Fires on every change; the same change goes out on the bus as <see cref="GameEvents.PlayerCarryChanged"/> for the HUD.</summary>
        public event Action Changed;

        private void Publish()
        {
            Changed?.Invoke();
            GameEvents.RaisePlayerCarryChanged(Describe());
        }

        public void Configure(int plates)
        {
            plateCapacity = Mathf.Max(1, plates);
            Publish();
        }

        /// <summary>Temporary carry limits from fortunes; zero or less clears an override. Already-held loads are not trimmed here.</summary>
        public void SetCapacityOverrides(int plates, int foodUnits)
        {
            _plateCapacityOverride = plates > 0 ? plates : -1;
            _foodCapacityOverride = foodUnits > 0 ? foodUnits : -1;
            Publish();
        }

        public bool IsHolding(string itemId) => IsHoldingItem && HeldItemId == itemId;

        // ----- Food -----

        public bool TryTakeFoodTray(FoodDefinition food, int units, bool raw = false)
        {
            if (!HandsFree || food == null || units <= 0) return false;
            HeldFood = food;
            HeldFoodUnits = units;
            HeldFoodRaw = raw;
            Publish();
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
                HeldFoodRaw = false;
            }
            Publish();
            return removed;
        }

        // ----- Plates -----

        public int AddPlates(int count)
        {
            if (HasNonPlateLoad) return 0;
            int added = Mathf.Clamp(count, 0, PlateCapacity - HeldPlates);
            HeldPlates += added;
            if (added > 0) Publish();
            return added;
        }

        public int RemovePlates(int count)
        {
            int removed = Mathf.Clamp(count, 0, HeldPlates);
            HeldPlates -= removed;
            if (removed > 0) Publish();
            return removed;
        }

        // ----- Items -----

        public bool TryTakeItem(string itemId, string displayName, int uses = 1, bool blocksSprint = false, bool fragile = false)
        {
            if (!HandsFree || string.IsNullOrEmpty(itemId)) return false;
            HeldItemId = itemId;
            HeldItemName = string.IsNullOrEmpty(displayName) ? itemId : displayName;
            HeldItemUses = Mathf.Max(1, uses);
            HeldItemBlocksSprint = blocksSprint;
            HeldItemFragile = fragile;
            Publish();
            return true;
        }

        /// <summary>Spends one use of the held item if it matches; the last use empties your hands. False if you don't hold it.</summary>
        public bool UseItem(string itemId)
        {
            if (!IsHolding(itemId)) return false;
            HeldItemUses--;
            if (HeldItemUses <= 0) ClearItem();
            else Publish();
            return true;
        }

        public void ClearItem()
        {
            if (!IsHoldingItem) return;
            HeldItemId = null;
            HeldItemName = null;
            HeldItemUses = 0;
            HeldItemBlocksSprint = false;
            HeldItemFragile = false;
            Publish();
        }

        // ----- To-go box -----

        public bool TryTakeToGoBox()
        {
            if (!HandsFree) return false;
            IsHoldingToGoBox = true;
            ToGoUnits = 0;
            _toGo.Clear();
            _toGoOrder.Clear();
            Publish();
            return true;
        }

        public int CountInToGoBox(FoodDefinition food)
        {
            return food != null && _toGo.TryGetValue(food, out int units) ? units : 0;
        }

        /// <summary>Scoops one unit into the box unless that food or the box is already at its limit.</summary>
        public bool AddToGoUnit(FoodDefinition food, int maxPerItem, int maxUnits)
        {
            if (!IsHoldingToGoBox || food == null) return false;
            if (ToGoUnits >= maxUnits) return false;
            int current = CountInToGoBox(food);
            if (current >= maxPerItem) return false;
            if (current == 0) _toGoOrder.Add(food);
            _toGo[food] = current + 1;
            ToGoUnits++;
            Publish();
            return true;
        }

        /// <summary>Hands the box over (to the pickup rack): your hands are empty afterwards.</summary>
        public void TakeToGoBox()
        {
            if (!IsHoldingToGoBox) return;
            IsHoldingToGoBox = false;
            ToGoUnits = 0;
            _toGo.Clear();
            _toGoOrder.Clear();
            Publish();
        }

        // ----- Losing it all -----

        /// <summary>Empties your hands with no consequences (a station took the load).</summary>
        public void DropEverything()
        {
            HeldFood = null;
            HeldFoodUnits = 0;
            HeldFoodRaw = false;
            HeldPlates = 0;
            HeldItemId = null;
            HeldItemName = null;
            HeldItemUses = 0;
            HeldItemBlocksSprint = false;
            HeldItemFragile = false;
            IsHoldingToGoBox = false;
            ToGoUnits = 0;
            _toGo.Clear();
            _toGoOrder.Clear();
            Publish();
        }

        /// <summary>
        /// Everything in your hands hits the floor with the usual consequences: plates break (the
        /// economy charges for them), a lightbulb or glass pane shatters, food and to-go orders are
        /// lost. <paramref name="cause"/> starts the notice ("Slipped", "Dropped"). False if your hands were empty.
        /// </summary>
        public bool SpillLoad(Vector3 at, string cause)
        {
            if (HandsFree) return false;

            int plates = HeldPlates;
            string what;
            if (IsHoldingPlates) what = $"{plates} plate{(plates == 1 ? "" : "s")} hit the floor";
            else if (IsHoldingFood) what = $"{HeldFoodUnits} {HeldFood.DisplayName}{(HeldFoodRaw ? " (raw)" : "")} hit the floor";
            else if (IsHoldingToGoBox) what = ToGoUnits > 0 ? $"the to-go box ({ToGoUnits} units) hit the floor" : "the empty to-go box hit the floor";
            else if (HeldItemFragile) what = $"the {HeldItemName} shattered";
            else what = $"the {HeldItemName} clattered off somewhere";

            DropEverything();
            GameEvents.RaiseNotice($"{(string.IsNullOrEmpty(cause) ? "Dropped" : cause)}: {what}.");
            if (plates > 0) GameEvents.RaiseDishesBroken(plates, at);
            return true;
        }

        public string Describe()
        {
            if (IsHoldingFood)
                return HeldFoodRaw
                    ? $"Carrying: raw {HeldFood.DisplayName} box ({HeldFoodUnits} units)"
                    : $"Carrying: {HeldFood.DisplayName} x{HeldFoodUnits}" + (FoodCapacity != DefaultFoodCapacity ? $" (cap {FoodCapacity})" : "");
            if (IsHoldingPlates) return $"Carrying: {HeldPlates}/{PlateCapacity} dirty plates";
            if (IsHoldingToGoBox) return $"Carrying: to-go box {DescribeToGo()}";
            if (IsHoldingItem) return HeldItemUses > 1 ? $"Carrying: {HeldItemName} ({HeldItemUses} left)" : $"Carrying: {HeldItemName}";
            return PlateCapacity != plateCapacity ? $"Hands free (plate cap {PlateCapacity})" : "Hands free";
        }

        /// <summary>"2 Egg Rolls, 1 Lo Mein (3 units)" or "(empty)".</summary>
        public string DescribeToGo()
        {
            if (!IsHoldingToGoBox || ToGoUnits == 0) return "(empty)";
            var parts = new List<string>(_toGoOrder.Count);
            for (int i = 0; i < _toGoOrder.Count; i++)
                parts.Add($"{_toGo[_toGoOrder[i]]} {_toGoOrder[i].DisplayName}");
            return $"{string.Join(", ", parts)} ({ToGoUnits} units)";
        }
    }
}
