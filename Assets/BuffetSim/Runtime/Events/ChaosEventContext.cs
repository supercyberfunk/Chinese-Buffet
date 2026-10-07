using System;
using System.Collections.Generic;
using BuffetSim.Buffet;
using BuffetSim.Customers;
using BuffetSim.Economy;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// Everything a chaos event needs to run, handed over once by the scene bootstrap: the numbers,
    /// the RNG, a few landmarks of the level and a read-only way to find customers. Plain data with
    /// no behaviour, so events never hold references to the systems themselves.
    /// </summary>
    public sealed class ChaosEventContext
    {
        private static readonly CustomerAgent[] NoCustomers = new CustomerAgent[0];
        private static readonly IFoodSource[] NoSources = new IFoodSource[0];
        private static readonly Vector3[] NoPoints = new Vector3[0];

        public EconomyConfig Config;
        public System.Random Rng = new System.Random();
        public Font Font;
        /// <summary>On the sidewalk: where customers spawn and where thieves and rats are sent.</summary>
        public Vector3 DoorOutside;
        /// <summary>Just inside the entrance.</summary>
        public Vector3 DoorInside;
        public Vector3 RegisterPoint;
        /// <summary>The walkable dining area; wanderers and spills stay inside it.</summary>
        public Bounds FloorBounds;
        public CustomerQueue Queue;
        /// <summary>The spawner's live list of everyone in the building. Entries can be null for a frame after a customer is destroyed.</summary>
        public Func<IReadOnlyList<CustomerAgent>> CustomersInStore;
        /// <summary>Every tray on the buffet line (raided by Mongolians, torched by dragons).</summary>
        public Func<IReadOnlyList<IFoodSource>> FoodSources;
        public Transform Player;

        // Landmarks the events are built around.
        /// <summary>Centre of the front window, inside face (the rock comes through here; the pane goes back here).</summary>
        public Vector3 WindowPoint;
        /// <summary>Where the pitcher boy comes through the wall.</summary>
        public Vector3 WallHolePoint;
        /// <summary>The restroom door (the toilet is behind it, and so is the pipe).</summary>
        public Vector3 RestroomPoint;
        /// <summary>On the dining-room side of the buffet line, in the middle.</summary>
        public Vector3 BuffetPoint;
        /// <summary>The gap in the kitchen divider.</summary>
        public Vector3 KitchenPoint;
        public Vector3 DishwasherPoint;
        /// <summary>Table centres that have a shower drain under them.</summary>
        public Vector3[] DrainPoints;
        /// <summary>The front window: breakable, fixed with a glass pane.</summary>
        public BreakablePanel Window;
        /// <summary>The left wall by the tables: breakable, fixed with duct tape.</summary>
        public BreakablePanel WallHole;

        /// <summary>Null-safe <see cref="CustomersInStore"/>: an empty list when nothing is wired up.</summary>
        public IReadOnlyList<CustomerAgent> GetCustomersInStore()
        {
            IReadOnlyList<CustomerAgent> list = CustomersInStore != null ? CustomersInStore() : null;
            return list ?? NoCustomers;
        }

        public IReadOnlyList<IFoodSource> GetFoodSources()
        {
            IReadOnlyList<IFoodSource> list = FoodSources != null ? FoodSources() : null;
            return list ?? NoSources;
        }

        public IReadOnlyList<Vector3> GetDrainPoints() => DrainPoints ?? NoPoints;

        /// <summary>The player's inventory, or null when there is no player.</summary>
        public T PlayerComponent<T>() where T : Component
        {
            return Player != null ? Player.GetComponent<T>() : null;
        }
    }
}
