using UnityEngine;

namespace BuffetSim.Food
{
    public enum FoodKind
    {
        /// <summary>Countable items such as egg rolls or dumplings.</summary>
        SingleUnit,
        /// <summary>Scooped dishes such as kung pao chicken or fried rice.</summary>
        Serving,
    }

    /// <summary>One base food the store can stock. Data only; no behaviour.</summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Food Definition", fileName = "Food")]
    public sealed class FoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private FoodKind kind = FoodKind.SingleUnit;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Color Color => color;
        public FoodKind Kind => kind;

        public static FoodDefinition Create(string foodName, Color foodColor, FoodKind foodKind)
        {
            var food = CreateInstance<FoodDefinition>();
            food.name = foodName;
            food.displayName = foodName;
            food.color = foodColor;
            food.kind = foodKind;
            return food;
        }
    }
}
