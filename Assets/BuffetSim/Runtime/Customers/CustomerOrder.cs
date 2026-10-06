using System.Collections.Generic;
using BuffetSim.Economy;
using BuffetSim.Food;
using UnityEngine;

namespace BuffetSim.Customers
{
    /// <summary>
    /// The RNG from the design notes, as plain C#: roll how many dishes (2..6, at least one
    /// countable and one scooped when possible), roll total units (5..20), then spread the units
    /// across the dishes with no single dish above 10.
    /// </summary>
    public sealed class CustomerOrder
    {
        public readonly struct Line
        {
            public readonly FoodDefinition Food;
            public readonly int Units;

            public Line(FoodDefinition food, int units)
            {
                Food = food;
                Units = units;
            }
        }

        private readonly List<Line> _lines = new List<Line>();

        public IReadOnlyList<Line> Lines => _lines;
        public int TotalUnits { get; private set; }

        public static CustomerOrder Roll(IReadOnlyList<FoodDefinition> unlocked, EconomyConfig config, System.Random rng)
        {
            var order = new CustomerOrder();
            if (unlocked == null || unlocked.Count == 0 || config == null) return order;

            int maxDishes = Mathf.Clamp(config.MaxDishesPerOrder, 1, unlocked.Count);
            int minDishes = Mathf.Clamp(config.MinDishesPerOrder, 1, maxDishes);
            int dishCount = rng.Next(minDishes, maxDishes + 1);

            var pool = new List<FoodDefinition>(unlocked);
            var chosen = new List<FoodDefinition>(dishCount);

            // Notes: "one single unit dish and one serving unit dish" as the minimum pair.
            PickOfKind(pool, chosen, FoodKind.SingleUnit, rng);
            if (chosen.Count < dishCount) PickOfKind(pool, chosen, FoodKind.Serving, rng);
            while (chosen.Count < dishCount && pool.Count > 0)
            {
                int index = rng.Next(pool.Count);
                chosen.Add(pool[index]);
                pool.RemoveAt(index);
            }

            int maxPerItem = Mathf.Max(1, config.MaxUnitsPerItem);
            int totalUnits = rng.Next(config.MinUnitsPerVisit, config.MaxUnitsPerVisit + 1);
            totalUnits = Mathf.Clamp(totalUnits, chosen.Count, chosen.Count * maxPerItem);

            var units = new int[chosen.Count];
            for (int i = 0; i < units.Length; i++) units[i] = 1;

            int remaining = totalUnits - chosen.Count;
            while (remaining > 0)
            {
                int index = rng.Next(units.Length);
                if (units[index] >= maxPerItem) continue; // total is capped above, so this terminates
                units[index]++;
                remaining--;
            }

            for (int i = 0; i < chosen.Count; i++)
                order._lines.Add(new Line(chosen[i], units[i]));
            order.TotalUnits = totalUnits;
            return order;
        }

        private static void PickOfKind(List<FoodDefinition> pool, List<FoodDefinition> chosen, FoodKind kind, System.Random rng)
        {
            var candidates = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].Kind == kind) candidates.Add(i);
            }
            if (candidates.Count == 0) return;

            int poolIndex = candidates[rng.Next(candidates.Count)];
            chosen.Add(pool[poolIndex]);
            pool.RemoveAt(poolIndex);
        }

        public string Describe()
        {
            if (_lines.Count == 0) return "nothing";
            var parts = new List<string>(_lines.Count);
            foreach (Line line in _lines) parts.Add($"{line.Units} {line.Food.DisplayName}");
            return string.Join(", ", parts);
        }
    }
}
