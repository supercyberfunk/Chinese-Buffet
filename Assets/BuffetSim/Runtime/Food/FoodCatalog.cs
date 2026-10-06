using System.Collections.Generic;
using UnityEngine;

namespace BuffetSim.Food
{
    /// <summary>
    /// The foods the player has unlocked. Customers roll against everything in here, not just
    /// what happens to be on the buffet line, which is what punishes under-stocking.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Food Catalog", fileName = "FoodCatalog")]
    public sealed class FoodCatalog : ScriptableObject
    {
        [SerializeField] private List<FoodDefinition> unlockedFoods = new List<FoodDefinition>();

        public IReadOnlyList<FoodDefinition> Unlocked => unlockedFoods;

        public void SetFoods(IEnumerable<FoodDefinition> foods)
        {
            unlockedFoods = new List<FoodDefinition>(foods);
        }

        /// <summary>Six starter foods for the demo: three countable, three scooped.</summary>
        public static FoodCatalog CreateDefault()
        {
            var catalog = CreateInstance<FoodCatalog>();
            catalog.name = "FoodCatalog (runtime default)";
            catalog.unlockedFoods = new List<FoodDefinition>
            {
                FoodDefinition.Create("Egg Rolls", new Color(0.85f, 0.55f, 0.20f), FoodKind.SingleUnit),
                FoodDefinition.Create("Dumplings", new Color(0.93f, 0.88f, 0.75f), FoodKind.SingleUnit),
                FoodDefinition.Create("Crab Rangoon", new Color(0.95f, 0.78f, 0.35f), FoodKind.SingleUnit),
                FoodDefinition.Create("Kung Pao Chicken", new Color(0.70f, 0.18f, 0.12f), FoodKind.Serving),
                FoodDefinition.Create("Lo Mein", new Color(0.90f, 0.80f, 0.30f), FoodKind.Serving),
                FoodDefinition.Create("Fried Rice", new Color(0.80f, 0.70f, 0.45f), FoodKind.Serving),
            };
            return catalog;
        }
    }
}
