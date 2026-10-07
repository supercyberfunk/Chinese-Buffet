using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using BuffetSim.UI;
using UnityEngine;

namespace BuffetSim.Items
{
    /// <summary>A tray (or Tupperware) of one food lying on the floor. E with free hands picks it up. Gone at close.</summary>
    public sealed class FoodTrayPickup : MonoBehaviour, IInteractable
    {
        private FoodDefinition _food;
        private int _units;
        private bool _raw;
        private string _what;

        public static FoodTrayPickup Spawn(FoodDefinition food, int units, Vector3 position, string what = "tray", bool raw = false, Transform parent = null)
        {
            var go = new GameObject($"{what} of {food.DisplayName}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, 0.05f, position.z);
            PrimitiveFactory.Visual("Tray", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.36f), MaterialLibrary.Get(new Color(0.85f, 0.88f, 0.92f)));
            PrimitiveFactory.Visual("Food", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.42f, 0.08f, 0.3f), MaterialLibrary.Get(food.Color));
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.2f, 0f);
            trigger.size = new Vector3(0.7f, 0.5f, 0.6f);
            var pickup = go.AddComponent<FoodTrayPickup>();
            pickup._food = food;
            pickup._units = units;
            pickup._raw = raw;
            pickup._what = what;
            return pickup;
        }

        private void OnEnable()
        {
            GameEvents.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            GameEvents.DayEnded -= OnDayEnded;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_food == null) return string.Empty;
            return inventory.HandsFree ? $"[E] Pick up the {_what} ({_units} {(_raw ? "raw " : "")}{_food.DisplayName})" : $"A {_what} of {_food.DisplayName} - hands full";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_food == null || !inventory.HandsFree) return;
            int carried = Mathf.Min(_units, inventory.FoodCapacity);
            if (!inventory.TryTakeFoodTray(_food, carried, _raw)) return;
            GameEvents.RaiseNotice($"Picked up {carried} {_food.DisplayName}{(carried < _units ? $"; {_units - carried} stayed on the floor" : "")}.");
            _units -= carried;
            if (_units <= 0) Destroy(gameObject);
        }

        private void OnDayEnded(int day)
        {
            Destroy(gameObject);
        }
    }
}
