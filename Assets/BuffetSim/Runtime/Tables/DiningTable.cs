using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Tables
{
    /// <summary>
    /// A table is free only when nobody has it reserved and every dirty plate has been cleared.
    /// A customer leaving rolls 2..5 plates onto it; the player clears them (max 4 carried).
    /// </summary>
    public sealed class DiningTable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform seatPoint;
        [SerializeField] private Transform plateStackRoot;
        [SerializeField] private Renderer statusLight;
        [SerializeField] private Renderer lamp;

        private readonly List<GameObject> _plateVisuals = new List<GameObject>();
        private object _reservedBy;
        private int _dirtyPlates;

        public bool IsReserved => _reservedBy != null;
        public int DirtyPlates => _dirtyPlates;
        public bool IsAvailable => _reservedBy == null && _dirtyPlates == 0;
        public Vector3 SeatPosition => seatPoint != null ? seatPoint.position : transform.position + transform.right;
        /// <summary>The lamp hanging over the table (the booth-lamp event turns it off), or null.</summary>
        public Renderer Lamp => lamp;

        public void Configure(Transform seat, Transform plateRoot, Renderer statusRenderer, Renderer lampRenderer = null)
        {
            seatPoint = seat;
            plateStackRoot = plateRoot;
            statusLight = statusRenderer;
            lamp = lampRenderer;
            RefreshVisuals();
        }

        public bool TryReserve(object customer)
        {
            if (!IsAvailable || customer == null) return false;
            _reservedBy = customer;
            RefreshVisuals();
            return true;
        }

        public void Release(object customer, int dirtyPlates)
        {
            if (_reservedBy == customer) _reservedBy = null;
            _dirtyPlates += Mathf.Max(0, dirtyPlates);
            RefreshVisuals();
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_dirtyPlates > 0)
            {
                if (inventory.HasNonPlateLoad) return $"Dirty table ({_dirtyPlates} plates) - hands full";
                if (inventory.HeldPlates >= inventory.PlateCapacity) return $"Dirty table ({_dirtyPlates} plates) - you can't carry more";
                return $"[E] Clear plates ({_dirtyPlates} on table, carrying {inventory.HeldPlates}/{inventory.PlateCapacity})";
            }
            return IsReserved ? "Table taken" : "Clean table";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_dirtyPlates <= 0 || inventory.HasNonPlateLoad) return;
            int taken = inventory.AddPlates(_dirtyPlates);
            if (taken <= 0)
            {
                GameEvents.RaiseNotice("You can't carry any more plates. Take them to the dishwasher.");
                return;
            }
            _dirtyPlates -= taken;
            GameEvents.RaiseNotice(_dirtyPlates > 0
                ? $"Picked up {taken} plates, {_dirtyPlates} still on the table."
                : $"Picked up {taken} plates. Table is clean.");
            RefreshVisuals();
        }

        /// <summary>Someone else cleared it (a cousin in a visor): every plate comes off. Returns how many there were.</summary>
        public int ClearAllPlates()
        {
            int plates = _dirtyPlates;
            _dirtyPlates = 0;
            RefreshVisuals();
            return plates;
        }

        private void RefreshVisuals()
        {
            if (plateStackRoot != null)
            {
                while (_plateVisuals.Count < _dirtyPlates)
                {
                    int index = _plateVisuals.Count;
                    GameObject plate = PrimitiveFactory.Visual($"Plate {index + 1}", PrimitiveType.Cylinder, plateStackRoot,
                        new Vector3(0f, 0.015f + 0.03f * index, 0f), new Vector3(0.3f, 0.012f, 0.3f), MaterialLibrary.Get(new Color(0.92f, 0.92f, 0.9f)));
                    _plateVisuals.Add(plate);
                }
                for (int i = 0; i < _plateVisuals.Count; i++) _plateVisuals[i].SetActive(i < _dirtyPlates);
            }

            if (statusLight != null)
            {
                // Notes: a full/empty cup as the "is this table done" tell. We use a coloured light for now.
                Color color = _dirtyPlates > 0 ? new Color(1f, 0.75f, 0.1f) : (IsReserved ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.85f, 0.3f));
                statusLight.sharedMaterial = MaterialLibrary.Get(color);
            }
        }
    }
}
