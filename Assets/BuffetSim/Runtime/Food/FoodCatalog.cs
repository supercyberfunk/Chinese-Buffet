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

        /// <summary>
        /// Runs when the asset loads (not on every Inspector edit, so a slot added with + can still be
        /// filled in): a deleted FoodDefinition asset leaves a null the scene builder would trip over.
        /// </summary>
        private void OnEnable()
        {
            RemoveMissingFoods();
        }

        /// <summary>Drops null entries and says so.</summary>
        private void RemoveMissingFoods()
        {
            if (unlockedFoods == null)
            {
                unlockedFoods = new List<FoodDefinition>();
                return;
            }
            int removed = unlockedFoods.RemoveAll(food => food == null);
            if (removed > 0)
                Debug.LogWarning($"[Buffet] FoodCatalog '{name}' had {removed} empty food slot{(removed == 1 ? "" : "s")} (deleted or unassigned FoodDefinition); removed.", this);
        }

        /// <summary>Six starter foods for the demo: three countable, three scooped, spread over the four cookers.</summary>
        public static FoodCatalog CreateDefault()
        {
            var catalog = CreateInstance<FoodCatalog>();
            catalog.name = "FoodCatalog (runtime default)";
            catalog.unlockedFoods = new List<FoodDefinition>
            {
                FoodDefinition.Create("Egg Rolls", new Color(0.85f, 0.55f, 0.20f), FoodKind.SingleUnit, CookerKind.Fryer),
                FoodDefinition.Create("Dumplings", new Color(0.93f, 0.88f, 0.75f), FoodKind.SingleUnit, CookerKind.Steamer),
                FoodDefinition.Create("Crab Rangoon", new Color(0.95f, 0.78f, 0.35f), FoodKind.SingleUnit, CookerKind.Fryer),
                FoodDefinition.Create("Kung Pao Chicken", new Color(0.70f, 0.18f, 0.12f), FoodKind.Serving, CookerKind.Wok),
                FoodDefinition.Create("Lo Mein", new Color(0.90f, 0.80f, 0.30f), FoodKind.Serving, CookerKind.Wok),
                FoodDefinition.Create("Fried Rice", new Color(0.80f, 0.70f, 0.45f), FoodKind.Serving, CookerKind.RiceCooker),
            };
            return catalog;
        }
    }
}
