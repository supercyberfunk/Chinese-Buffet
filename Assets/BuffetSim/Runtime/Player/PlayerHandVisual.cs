using BuffetSim.Bootstrap;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// Presentation only: shows a crude tray, raw box, plate stack, to-go box or held item in front
    /// of the camera based on the inventory. Subscribes to the inventory, never drives it.
    /// </summary>
    public sealed class PlayerHandVisual : MonoBehaviour
    {
        private static readonly Color RawBoxColor = new Color(0.85f, 0.88f, 0.92f);
        private static readonly Color ToGoColor = new Color(0.96f, 0.96f, 0.93f);

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Transform trayVisual;
        [SerializeField] private Renderer trayFoodRenderer;
        [SerializeField] private Transform plateStackVisual;
        [SerializeField] private Transform itemRoot;

        private GameObject _itemVisual;
        private string _itemVisualId;
        private GameObject _toGoVisual;
        private GameObject _rawBoxVisual;
        private Renderer _rawBoxStripe;

        public void Configure(PlayerInventory source, Transform tray, Renderer trayFood, Transform plates, Transform items = null)
        {
            if (inventory != null) inventory.Changed -= Refresh;
            inventory = source;
            trayVisual = tray;
            trayFoodRenderer = trayFood;
            plateStackVisual = plates;
            itemRoot = items;
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
            bool cooked = inventory != null && inventory.IsHoldingCookedFood;
            bool raw = inventory != null && inventory.IsHoldingRawFood;
            bool plates = inventory != null && inventory.IsHoldingPlates;
            bool toGo = inventory != null && inventory.IsHoldingToGoBox;
            bool item = inventory != null && inventory.IsHoldingItem;

            if (trayVisual != null) trayVisual.gameObject.SetActive(cooked);
            if (cooked && trayFoodRenderer != null)
            {
                trayFoodRenderer.sharedMaterial = MaterialLibrary.Get(inventory.HeldFood.Color);
                float fraction = Mathf.Clamp01(inventory.HeldFoodUnits / (float)PlayerInventory.DefaultFoodCapacity);
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

            if (itemRoot == null) return;

            // A frosty tupperware box with a stripe of the food's colour.
            if (raw && _rawBoxVisual == null)
            {
                _rawBoxVisual = PrimitiveFactory.Visual("Raw Box", PrimitiveType.Cube, itemRoot, Vector3.zero, new Vector3(0.42f, 0.2f, 0.3f), MaterialLibrary.Get(RawBoxColor));
                _rawBoxStripe = PrimitiveFactory.Visual("Stripe", PrimitiveType.Cube, _rawBoxVisual.transform, new Vector3(0f, 0.2f, -0.51f), new Vector3(1.01f, 0.3f, 0.02f), MaterialLibrary.Get(Color.white)).GetComponent<Renderer>();
                SetLayer(_rawBoxVisual, itemRoot.gameObject.layer);
            }
            if (_rawBoxVisual != null)
            {
                _rawBoxVisual.SetActive(raw);
                if (raw && _rawBoxStripe != null) _rawBoxStripe.sharedMaterial = MaterialLibrary.Get(inventory.HeldFood.Color);
            }

            // A white clamshell, open a crack.
            if (toGo && _toGoVisual == null)
            {
                _toGoVisual = PrimitiveFactory.Visual("To-Go Box", PrimitiveType.Cube, itemRoot, Vector3.zero, new Vector3(0.32f, 0.12f, 0.28f), MaterialLibrary.Get(ToGoColor));
                GameObject lid = PrimitiveFactory.Visual("Lid", PrimitiveType.Cube, _toGoVisual.transform, new Vector3(0f, 0.9f, 0.35f), new Vector3(1f, 0.15f, 0.9f), MaterialLibrary.Get(ToGoColor));
                lid.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                SetLayer(_toGoVisual, itemRoot.gameObject.layer);
            }
            if (_toGoVisual != null) _toGoVisual.SetActive(toGo);

            string wanted = item ? inventory.HeldItemId : null;
            if (_itemVisual != null && _itemVisualId != wanted)
            {
                Destroy(_itemVisual);
                _itemVisual = null;
                _itemVisualId = null;
            }
            if (item && _itemVisual == null)
            {
                _itemVisual = BuildItem(wanted);
                _itemVisualId = wanted;
                if (_itemVisual != null) SetLayer(_itemVisual, itemRoot.gameObject.layer);
            }
        }

        /// <summary>One crude primitive per item id; anything unknown is a grey block.</summary>
        private GameObject BuildItem(string id)
        {
            switch (id)
            {
                case CarryItems.Lightbulb:
                    GameObject bulb = PrimitiveFactory.Visual("Lightbulb", PrimitiveType.Sphere, itemRoot, Vector3.zero, Vector3.one * 0.12f, MaterialLibrary.Get(new Color(1f, 0.97f, 0.75f)));
                    PrimitiveFactory.Visual("Base", PrimitiveType.Cylinder, bulb.transform, new Vector3(0f, -0.6f, 0f), new Vector3(0.5f, 0.3f, 0.5f), MaterialLibrary.Get(new Color(0.6f, 0.6f, 0.6f)));
                    return bulb;
                case CarryItems.DuctTape:
                    GameObject tape = PrimitiveFactory.Visual("Duct Tape", PrimitiveType.Cylinder, itemRoot, Vector3.zero, new Vector3(0.16f, 0.04f, 0.16f), MaterialLibrary.Get(new Color(0.6f, 0.62f, 0.65f)));
                    tape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    return tape;
                case CarryItems.Wrench:
                    GameObject wrench = PrimitiveFactory.Visual("Wrench", PrimitiveType.Cube, itemRoot, Vector3.zero, new Vector3(0.05f, 0.03f, 0.35f), MaterialLibrary.Get(new Color(0.5f, 0.52f, 0.55f)));
                    PrimitiveFactory.Visual("Jaw", PrimitiveType.Cube, wrench.transform, new Vector3(0f, 0f, 0.55f), new Vector3(2.4f, 1f, 0.15f), MaterialLibrary.Get(new Color(0.5f, 0.52f, 0.55f)));
                    return wrench;
                case CarryItems.GlassPane:
                    // Big and in the way, on purpose.
                    return PrimitiveFactory.Visual("Glass Pane", PrimitiveType.Cube, itemRoot, new Vector3(-0.2f, 0.15f, 0.15f), new Vector3(0.9f, 0.7f, 0.02f), MaterialLibrary.Get(new Color(0.7f, 0.85f, 0.95f)));
                case CarryItems.Tarp:
                    return PrimitiveFactory.Visual("Tarp", PrimitiveType.Cube, itemRoot, Vector3.zero, new Vector3(0.45f, 0.2f, 0.3f), MaterialLibrary.Get(new Color(0.85f, 0.15f, 0.12f)));
                case CarryItems.LoadedPlate:
                    GameObject plate = PrimitiveFactory.Visual("Loaded Plate", PrimitiveType.Cylinder, itemRoot, Vector3.zero, new Vector3(0.3f, 0.012f, 0.3f), MaterialLibrary.Get(new Color(0.92f, 0.92f, 0.9f)));
                    PrimitiveFactory.Visual("Food", PrimitiveType.Sphere, plate.transform, new Vector3(0f, 3f, 0f), new Vector3(0.6f, 5f, 0.6f), MaterialLibrary.Get(new Color(0.85f, 0.55f, 0.2f)));
                    return plate;
                default:
                    return PrimitiveFactory.Visual(string.IsNullOrEmpty(id) ? "Item" : id, PrimitiveType.Cube, itemRoot, Vector3.zero, Vector3.one * 0.18f, MaterialLibrary.Get(new Color(0.5f, 0.5f, 0.5f)));
            }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }
    }
}
