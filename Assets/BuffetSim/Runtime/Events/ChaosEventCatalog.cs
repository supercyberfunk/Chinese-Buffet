using System.Collections.Generic;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// The set of chaos events the scheduler can pick from. Make one as an asset and fill it with
    /// event assets, or use <see cref="CreateDefault"/> for runtime instances of the built-in four.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Event Catalog", fileName = "ChaosEventCatalog")]
    public sealed class ChaosEventCatalog : ScriptableObject
    {
        [SerializeField] private List<ChaosEvent> events = new List<ChaosEvent>();

        public IReadOnlyList<ChaosEvent> Events => events;
        public int Count => events.Count;

        /// <summary>The event whose id (asset or instance name) matches, or null.</summary>
        public ChaosEvent Find(string id)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null && events[i].name == id) return events[i];
            }
            return null;
        }

        /// <summary>Runtime instances of the four built-in events with their default tuning; no assets needed.</summary>
        public static ChaosEventCatalog CreateDefault()
        {
            var catalog = CreateInstance<ChaosEventCatalog>();
            catalog.name = "ChaosEventCatalog (runtime default)";
            catalog.events = new List<ChaosEvent>
            {
                BusinessIsBoomingEvent.CreateDefault(),
                RatSummonerEvent.CreateDefault(),
                SpillEvent.CreateDefault(),
                LeprechaunEvent.CreateDefault(),
            };
            return catalog;
        }
    }
}
