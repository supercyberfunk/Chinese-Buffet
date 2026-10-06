using System;
using System.Collections.Generic;
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
        public Transform Player;

        /// <summary>Null-safe <see cref="CustomersInStore"/>: an empty list when nothing is wired up.</summary>
        public IReadOnlyList<CustomerAgent> GetCustomersInStore()
        {
            IReadOnlyList<CustomerAgent> list = CustomersInStore != null ? CustomersInStore() : null;
            return list ?? NoCustomers;
        }
    }
}
