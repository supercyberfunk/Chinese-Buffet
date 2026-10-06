using BuffetSim.Bootstrap;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// Presentation only: shows a crude tray or plate stack in front of the camera based on the
    /// inventory. Subscribes to the inventory, never drives it.
    /// </summary>
    public sealed class PlayerHandVisual : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Transform trayVisual;
        [SerializeField] private Renderer trayFoodRenderer;
        [SerializeField] private Transform plateStackVisual;

        public void Configure(PlayerInventory source, Transform tray, Renderer trayFood, Transform plates)
        {
            if (inventory != null) inventory.Changed -= Refresh;
            inventory = source;
            trayVisual = tray;
            trayFoodRenderer = trayFood;
            plateStackVisual = plates;
            if (inventory != null) inventory.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Refresh;
        }

        private void Refresh()
        {
            bool food = inventory != null && inventory.IsHoldingFood;
            bool plates = inventory != null && inventory.IsHoldingPlates;

            if (trayVisual != null) trayVisual.gameObject.SetActive(food);
            if (food && trayFoodRenderer != null)
            {
                trayFoodRenderer.sharedMaterial = MaterialLibrary.Get(inventory.HeldFood.Color);
                float fraction = Mathf.Clamp01(inventory.HeldFoodUnits / 20f);
                Vector3 scale = trayFoodRenderer.transform.localScale;
                trayFoodRenderer.transform.localScale = new Vector3(scale.x, Mathf.Max(0.01f, 0.08f * fraction), scale.z);
            }

            if (plateStackVisual != null)
            {
                plateStackVisual.gameObject.SetActive(plates);
                if (plates)
                {
                    Vector3 scale = plateStackVisual.localScale;
                    plateStackVisual.localScale = new Vector3(scale.x, 0.02f * inventory.HeldPlates, scale.z);
                }
            }
        }
    }
}
