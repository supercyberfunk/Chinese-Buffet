using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Items
{
    /// <summary>
    /// Something small lying on the floor that goes in the apron pocket (a rock, a dodgeball, a pack
    /// of cigarettes, a fortune cookie). Press E to pick it up. Anything still on the floor is mopped
    /// up when the day ends, unless it was spawned to stay overnight.
    /// </summary>
    public sealed class PocketPickup : MonoBehaviour, IInteractable
    {
        private const float RestLift = 0.02f;

        private string _id;
        private string _displayName;
        private int _count;
        private int _max;
        private bool _keepOvernight;
        private bool _taken;

        public string ItemId => _id;

        /// <summary>
        /// Drops a pickup on the floor at <paramref name="position"/> with a crude primitive look. The
        /// trigger collider is what the player's interaction ray finds.
        /// </summary>
        public static PocketPickup Spawn(string id, string displayName, int count, int max, Vector3 position, Color color,
            PrimitiveType shape = PrimitiveType.Sphere, float size = 0.25f, bool keepOvernight = false, Transform parent = null)
        {
            var root = new GameObject($"Pickup - {displayName}");
            if (parent != null) root.transform.SetParent(parent, true);
            float floor = FloorHeightAt(position);
            root.transform.position = new Vector3(position.x, floor + RestLift, position.z);
            root.layer = 0;

            PrimitiveFactory.Visual("Visual", shape, root.transform, new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, MaterialLibrary.Get(color));

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.3f, size);
            trigger.center = new Vector3(0f, size * 0.5f, 0f);

            PocketPickup pickup = root.AddComponent<PocketPickup>();
            pickup._id = id;
            pickup._displayName = displayName;
            pickup._count = Mathf.Max(1, count);
            pickup._max = Mathf.Max(1, max);
            pickup._keepOvernight = keepOvernight;
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
            if (_taken) return string.Empty;
            PlayerPocket pocket = inventory != null ? inventory.GetComponent<PlayerPocket>() : null;
            if (pocket != null && !pocket.HasRoomFor(_id, _max)) return $"{_displayName} - your pocket is full of those";
            return _count > 1 ? $"[E] Pick up {_displayName} x{_count}" : $"[E] Pick up {_displayName}";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_taken || inventory == null) return;
            PlayerPocket pocket = inventory.GetComponent<PlayerPocket>();
            if (pocket == null) return;
            int added = pocket.Add(_id, _displayName, _count, _max);
            if (added <= 0) return;

            _count -= added;
            GameEvents.RaiseNotice(added > 1 ? $"Pocketed {added} {_displayName}." : $"Pocketed the {_displayName}.");
            if (_count > 0) return;
            _taken = true;
            Destroy(gameObject);
        }

        private void OnDayEnded(int day)
        {
            if (!_keepOvernight && !_taken) Destroy(gameObject);
        }

        private static float FloorHeightAt(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }
    }
}
