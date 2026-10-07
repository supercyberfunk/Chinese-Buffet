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

    /// <summary>Which kitchen station turns a raw box of this food into a cooked tray.</summary>
    public enum CookerKind
    {
        Fryer,
        Wok,
        Steamer,
        RiceCooker,
    }

    public static class CookerKindExtensions
    {
        public static string DisplayName(this CookerKind kind)
        {
            switch (kind)
            {
                case CookerKind.Fryer: return "deep fryer";
                case CookerKind.Wok: return "wok";
                case CookerKind.Steamer: return "steamer";
                case CookerKind.RiceCooker: return "rice cooker";
                default: return kind.ToString();
            }
        }
    }

    /// <summary>One base food the store can stock. Data only; no behaviour.</summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Food Definition", fileName = "Food")]
    public sealed class FoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private FoodKind kind = FoodKind.SingleUnit;
        [SerializeField] private CookerKind cooker = CookerKind.Fryer;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Color Color => color;
        public FoodKind Kind => kind;
        public CookerKind Cooker => cooker;

        public static FoodDefinition Create(string foodName, Color foodColor, FoodKind foodKind, CookerKind foodCooker = CookerKind.Fryer)
        {
            var food = CreateInstance<FoodDefinition>();
            food.name = foodName;
            food.displayName = foodName;
            food.color = foodColor;
            food.kind = foodKind;
            food.cooker = foodCooker;
            return food;
        }
    }
}
