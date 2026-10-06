using System.Collections.Generic;
using BuffetSim.Customers;
using BuffetSim.Food;
using BuffetSim.Tables;
using UnityEngine;

namespace BuffetSim.Buffet
{
    /// <summary>
    /// Registry of what's on the floor (trays, tables) exposed to customers through narrow
    /// interfaces. Customers never see the concrete tray or table lists.
    /// </summary>
    public sealed class BuffetFloor : MonoBehaviour, IBuffetDirectory, ITableDirectory
    {
        private readonly List<IFoodSource> _sources = new List<IFoodSource>();
        private readonly List<DiningTable> _tables = new List<DiningTable>();
        private readonly List<DiningTable> _scratch = new List<DiningTable>();
        private readonly System.Random _rng = new System.Random();

        public int TableCount => _tables.Count;

        public void RegisterSource(IFoodSource source)
        {
            if (source != null && !_sources.Contains(source)) _sources.Add(source);
        }

        public void RegisterTable(DiningTable table)
        {
            if (table != null && !_tables.Contains(table)) _tables.Add(table);
        }

        /// <summary>
        /// Prefers a tray that still has the food; falls back to an empty tray of that food so the
        /// customer still walks over, finds nothing, and takes the deduction.
        /// </summary>
        public IFoodSource FindSourceFor(FoodDefinition food)
        {
            IFoodSource fallback = null;
            for (int i = 0; i < _sources.Count; i++)
            {
                IFoodSource source = _sources[i];
                if (source.Food != food) continue;
                if (source.Units > 0) return source;
                if (fallback == null) fallback = source;
            }
            return fallback;
        }

        public bool TryReserveTable(object customer, out DiningTable table)
        {
            _scratch.Clear();
            for (int i = 0; i < _tables.Count; i++)
            {
                if (_tables[i].IsAvailable) _scratch.Add(_tables[i]);
            }

            if (_scratch.Count == 0)
            {
                table = null;
                return false;
            }

            table = _scratch[_rng.Next(_scratch.Count)];
            return table.TryReserve(customer);
        }
    }
}
