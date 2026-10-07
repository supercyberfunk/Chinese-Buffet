using System.Collections;
using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.ToGo
{
    /// <summary>
    /// The to-go shelf by the front door. Put a packed box on it and the order is "picked up" on
    /// the spot: the shelf asks the phone service over the bus whether the box matches an open
    /// order, and the money arrives the way the notes wanted, with nobody actually turning up.
    /// </summary>
    public sealed class PickupRack : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform boxSpot;

        private bool _hasOrder;
        private string _caller = string.Empty;

        private void OnEnable()
        {
            GameEvents.ToGoOrderPlaced += OnOrderPlaced;
            GameEvents.ToGoOrderEnded += OnOrderEnded;
        }

        private void OnDisable()
        {
            GameEvents.ToGoOrderPlaced -= OnOrderPlaced;
            GameEvents.ToGoOrderEnded -= OnOrderEnded;
        }

        public void Configure(Transform spot)
        {
            boxSpot = spot;
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (!inventory.IsHoldingToGoBox)
                return _hasOrder ? $"To-go pickup shelf - {_caller}'s order is open" : "To-go pickup shelf";
            if (!_hasOrder) return "Pickup shelf: nobody has ordered (Q to drop the box, or trash it)";
            return $"[E] Rack the box for {_caller}: {inventory.DescribeToGo()}";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (!inventory.IsHoldingToGoBox) return;

            IReadOnlyList<FoodDefinition> foods = inventory.ToGoFoods;
            var request = new ToGoDeliveryRequest
            {
                Foods = new FoodDefinition[foods.Count],
                Units = new int[foods.Count],
                WorldPosition = transform.position + Vector3.up,
            };
            for (int i = 0; i < foods.Count; i++)
            {
                request.Foods[i] = foods[i];
                request.Units[i] = inventory.CountInToGoBox(foods[i]);
            }

            GameEvents.RaiseToGoDeliveryRequested(request);
            if (!request.Accepted)
            {
                GameEvents.RaiseNotice(string.IsNullOrEmpty(request.Outcome) ? "The shelf doesn't want it." : request.Outcome);
                return;
            }

            inventory.TakeToGoBox();
            StartCoroutine(ShowBox());
        }

        /// <summary>A box sits on the shelf for a moment, then it's gone, because somebody came for it. Nobody saw who.</summary>
        private IEnumerator ShowBox()
        {
            Transform parent = boxSpot != null ? boxSpot : transform;
            GameObject box = PrimitiveFactory.Visual("Racked Box", PrimitiveType.Cube, parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.35f, 0.18f, 0.3f), MaterialLibrary.Get(new Color(0.95f, 0.95f, 0.9f)));
            PrimitiveFactory.Visual("Staple", PrimitiveType.Cube, box.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.3f, 0.05f, 0.1f), MaterialLibrary.Get(new Color(0.8f, 0.2f, 0.2f)));
            yield return new WaitForSeconds(4f);
            if (box != null) Destroy(box);
        }

        private void OnOrderPlaced(ToGoOrderInfo order)
        {
            _hasOrder = true;
            _caller = order.CallerName;
        }

        private void OnOrderEnded(ToGoOrderInfo order, bool delivered, string outcome)
        {
            _hasOrder = false;
            _caller = string.Empty;
        }
    }
}
