using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Food;
using BuffetSim.Tables;
using UnityEngine;

namespace BuffetSim.Buffet
{
    /// <summary>
    /// Registry of what's on the floor (trays, tables) exposed to customers through narrow
    /// interfaces. Customers never see the concrete tray or table lists. Also answers
    /// "refill the nearest tray" requests from fortunes.
    /// </summary>
    public sealed class BuffetFloor : MonoBehaviour, IBuffetDirectory, ITableDirectory
    {
        private readonly List<IFoodSource> _sources = new List<IFoodSource>();
        private readonly List<DiningTable> _tables = new List<DiningTable>();
        private readonly List<DiningTable> _scratch = new List<DiningTable>();
        private readonly System.Random _rng = new System.Random();

        public int TableCount => _tables.Count;
        /// <summary>Every food source on the line (chaos events raid these).</summary>
        public IReadOnlyList<IFoodSource> Sources => _sources;
        public IReadOnlyList<DiningTable> Tables => _tables;

        private void OnEnable()
        {
            GameEvents.TrayRefillRequested += OnTrayRefillRequested;
        }

        private void OnDisable()
        {
            GameEvents.TrayRefillRequested -= OnTrayRefillRequested;
        }

        /// <summary>"The pot is deeper than you think": the tray nearest the point fills to the top.</summary>
        private void OnTrayRefillRequested(Vector3 near)
        {
            IFoodSource best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < _sources.Count; i++)
            {
                IFoodSource source = _sources[i];
                if (source == null || source.Units >= source.Capacity) continue;
                float distance = (source.StandPosition - near).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = source;
                }
            }

            if (best == null)
            {
                GameEvents.RaiseNotice("Every tray is already full. The pot is exactly as deep as it looks.");
                return;
            }
            int added = best.Refill(best.Capacity - best.Units);
            string foodName = best.Food != null ? best.Food.DisplayName : "mystery";
            GameEvents.RaiseNotice($"The {foodName} tray refilled itself with {added} units. Nobody saw where they came from.");
        }

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
