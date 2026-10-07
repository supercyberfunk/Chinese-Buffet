using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Gambling
{
    /// <summary>
    /// The corkboard by the restroom door with forty pushpins. Every cracked slip you haven't pinned
    /// yet waits in your apron; E at the board pins them all. At forty pins the slot machine's lock
    /// pops, which it hears about over the bus.
    /// </summary>
    public sealed class FortuneWall : MonoBehaviour, IInteractable
    {
        private static readonly Color PinEmpty = new Color(0.45f, 0.42f, 0.4f);
        private static readonly Color[] PinColors =
        {
            new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.5f, 0.9f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.75f, 0.35f), new Color(0.9f, 0.5f, 0.1f),
        };

        [SerializeField] private int total = 40;
        [SerializeField] private TextMesh label;

        private readonly List<Renderer> _pins = new List<Renderer>();
        private readonly HashSet<int> _pinned = new HashSet<int>();
        private readonly List<int> _unpinned = new List<int>();
        private readonly List<string> _unpinnedTexts = new List<string>();
        private bool _jackpot;

        public int Pinned => _pinned.Count;
        public int Unpinned => _unpinned.Count;

        private void OnEnable()
        {
            GameEvents.FortuneRevealed += OnRevealed;
        }

        private void OnDisable()
        {
            GameEvents.FortuneRevealed -= OnRevealed;
        }

        public void Configure(int fortuneCount, List<Renderer> pinRenderers, TextMesh statusLabel)
        {
            total = Mathf.Max(1, fortuneCount);
            _pins.Clear();
            if (pinRenderers != null) _pins.AddRange(pinRenderers);
            label = statusLabel;
            RefreshLabel();
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_unpinned.Count > 0) return $"[E] Pin {_unpinned.Count} fortune slip{(_unpinned.Count == 1 ? "" : "s")} ({_pinned.Count}/{total} on the wall)";
            return _jackpot ? $"Wall of fortune: full. A refund was possible." : $"Wall of fortune: {_pinned.Count}/{total} pinned";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_unpinned.Count == 0) return;
            int count = _unpinned.Count;
            string last = _unpinnedTexts[_unpinnedTexts.Count - 1];
            for (int i = 0; i < _unpinned.Count; i++)
            {
                if (!_pinned.Add(_unpinned[i])) continue;
                int slot = _pinned.Count - 1;
                if (slot < _pins.Count && _pins[slot] != null) _pins[slot].sharedMaterial = MaterialLibrary.Get(PinColors[slot % PinColors.Length]);
            }
            _unpinned.Clear();
            _unpinnedTexts.Clear();
            GameEvents.RaiseNotice(count == 1
                ? $"Pinned \"{last}\" to the wall. {_pinned.Count}/{total}."
                : $"Pinned {count} slips to the wall, \"{last}\" on top. {_pinned.Count}/{total}.");
            RefreshLabel();
            GameEvents.RaiseFortuneWallChanged(_pinned.Count, total, 0);

            if (_pinned.Count >= total && !_jackpot)
            {
                _jackpot = true;
                GameEvents.RaiseNotice("The wall is full. From across the room: a lock popping. Achievement: A Refund Is Possible.");
                GameEvents.RaiseSlotJackpotUnlocked();
            }
        }

        private void OnRevealed(FortuneReveal reveal)
        {
            if (_pinned.Contains(reveal.Id) || _unpinned.Contains(reveal.Id))
            {
                GameEvents.RaiseFortuneWallChanged(_pinned.Count, total, _unpinned.Count);
                return;
            }
            _unpinned.Add(reveal.Id);
            _unpinnedTexts.Add(reveal.Text);
            GameEvents.RaiseFortuneWallChanged(_pinned.Count, total, _unpinned.Count);
        }

        private void RefreshLabel()
        {
            if (label != null) label.text = $"WALL OF FORTUNE\n{_pinned.Count}/{total}";
        }
    }
}
