using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A chaos event definition: the designer-facing data (name, description, pick weight, earliest
    /// day, cooldown) on a ScriptableObject, plus the factory that creates the live runner. Each
    /// subclass pairs its data with a <see cref="ChaosEventRunner"/>; the scheduler only sees this type.
    /// </summary>
    public abstract class ChaosEvent : ScriptableObject
    {
        [SerializeField] private string displayName = "Chaos";
        [TextArea] [SerializeField] private string description = string.Empty;
        [Tooltip("Relative chance of being picked when several events are eligible.")]
        [SerializeField] private float weight = 1f;
        [Tooltip("The event cannot fire before this day.")]
        [SerializeField] private int minDay = 1;
        [Tooltip("Seconds after this event ends before it may fire again.")]
        [SerializeField] private float cooldownSeconds = 120f;

        public string DisplayName => displayName;
        public string Description => description;
        public float Weight => weight;
        public int MinDay => minDay;
        public float CooldownSeconds => cooldownSeconds;

        /// <summary>What presentation systems get on the bus. The id is the asset (or runtime instance) name.</summary>
        public ChaosEventInfo Info => new ChaosEventInfo { Id = name, DisplayName = displayName, Description = description };

        /// <summary>Creates a GameObject under <paramref name="parent"/>, adds the concrete runner and starts it.</summary>
        public abstract ChaosEventRunner Begin(ChaosEventContext context, Transform parent);

        /// <summary>Shared body for <see cref="Begin"/>: one GameObject named after the event carrying a runner of type <typeparamref name="T"/>.</summary>
        protected T BeginWith<T>(ChaosEventContext context, Transform parent) where T : ChaosEventRunner
        {
            var go = new GameObject($"Chaos Event - {displayName}");
            if (parent != null) go.transform.SetParent(parent, false);
            T runner = go.AddComponent<T>();
            runner.Begin(this, context);
            return runner;
        }

        /// <summary>Fills a runtime instance (no asset) with its defaults; used by <see cref="ChaosEventCatalog.CreateDefault"/>.</summary>
        protected void Configure(string id, string eventDisplayName, string eventDescription, float pickWeight, int earliestDay, float cooldown)
        {
            name = id;
            displayName = eventDisplayName;
            description = eventDescription;
            weight = pickWeight;
            minDay = earliestDay;
            cooldownSeconds = cooldown;
        }
    }
}
