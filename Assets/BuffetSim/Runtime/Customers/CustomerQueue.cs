using System.Collections.Generic;
using UnityEngine;

namespace BuffetSim.Customers
{
    /// <summary>
    /// The line at the front desk. Slot 0 is at this transform; later slots step along
    /// <see cref="direction"/>. Customers ask for their index and walk to that slot.
    /// </summary>
    public sealed class CustomerQueue : MonoBehaviour
    {
        [SerializeField] private Vector3 direction = Vector3.right;
        [SerializeField] private float spacing = 1.1f;

        private readonly List<CustomerAgent> _line = new List<CustomerAgent>();

        public int Count => _line.Count;

        public void Configure(Vector3 lineDirection, float slotSpacing)
        {
            direction = lineDirection.sqrMagnitude > 0.0001f ? lineDirection.normalized : Vector3.right;
            spacing = Mathf.Max(0.5f, slotSpacing);
        }

        public int Enqueue(CustomerAgent customer)
        {
            if (!_line.Contains(customer)) _line.Add(customer);
            return _line.IndexOf(customer);
        }

        public void Remove(CustomerAgent customer)
        {
            _line.Remove(customer);
        }

        public int IndexOf(CustomerAgent customer) => _line.IndexOf(customer);

        public Vector3 SlotPosition(int index)
        {
            return transform.position + direction.normalized * (spacing * Mathf.Max(0, index));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < 8; i++) Gizmos.DrawWireSphere(SlotPosition(i), 0.2f);
        }
    }
}
