using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>Ids of the things that live in the apron pocket.</summary>
    public static class PocketItems
    {
        public const string Rock = "rock";
        public const string Dodgeball = "dodgeball";
        public const string Cigarettes = "cigarettes";
        public const string Cookie = "cookie";
    }

    /// <summary>
    /// The apron pocket: small things carried on top of whatever is in your hands (a rock, dodgeballs,
    /// a pack of cigarettes, fortune cookies). Tab picks which one left click uses; R always cracks a
    /// cookie. Plain bookkeeping; <see cref="PlayerThrower"/> decides what using an item does.
    /// </summary>
    public sealed class PlayerPocket : MonoBehaviour
    {
        public sealed class Entry
        {
            public string Id;
            public string Name;
            public int Count;
            public int Max;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _selected;

        public IReadOnlyList<Entry> Entries => _entries;
        public bool IsEmpty => _entries.Count == 0;
        public Entry Selected => _entries.Count > 0 ? _entries[Mathf.Clamp(_selected, 0, _entries.Count - 1)] : null;

        public event Action Changed;

        public int Count(string id)
        {
            Entry entry = Find(id);
            return entry != null ? entry.Count : 0;
        }

        /// <summary>Adds up to <paramref name="count"/> (never past <paramref name="max"/>) and returns how many fit. New kinds become the selection.</summary>
        public int Add(string id, string displayName, int count, int max)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return 0;
            Entry entry = Find(id);
            if (entry == null)
            {
                entry = new Entry { Id = id, Name = string.IsNullOrEmpty(displayName) ? id : displayName, Count = 0, Max = Mathf.Max(1, max) };
                _entries.Add(entry);
                _selected = _entries.Count - 1;
            }
            else
            {
                entry.Max = Mathf.Max(entry.Max, max);
            }

            int added = Mathf.Clamp(count, 0, entry.Max - entry.Count);
            entry.Count += added;
            if (entry.Count <= 0) _entries.Remove(entry);
            ClampSelection();
            Changed?.Invoke();
            return added;
        }

        public bool HasRoomFor(string id, int max)
        {
            Entry entry = Find(id);
            return entry == null ? max > 0 : entry.Count < Mathf.Max(entry.Max, max);
        }

        /// <summary>Takes <paramref name="count"/> out if there are that many; an emptied kind leaves the pocket.</summary>
        public bool TryRemove(string id, int count = 1)
        {
            Entry entry = Find(id);
            if (entry == null || entry.Count < count) return false;
            entry.Count -= count;
            if (entry.Count <= 0) _entries.Remove(entry);
            ClampSelection();
            Changed?.Invoke();
            return true;
        }

        public void CycleSelection()
        {
            if (_entries.Count <= 1) return;
            _selected = (_selected + 1) % _entries.Count;
            Changed?.Invoke();
        }

        /// <summary>Everything goes (the mop takes what's left at close; aliens are not careful).</summary>
        public void Clear(string id)
        {
            Entry entry = Find(id);
            if (entry == null) return;
            _entries.Remove(entry);
            ClampSelection();
            Changed?.Invoke();
        }

        /// <summary>"Pocket: [Rock] Dodgeball x3  Cookie x2" with the selected one in brackets; empty when nothing is carried.</summary>
        public string Describe()
        {
            if (_entries.Count == 0) return string.Empty;
            var parts = new List<string>(_entries.Count);
            Entry selected = Selected;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                string text = e.Count > 1 ? $"{e.Name} x{e.Count}" : e.Name;
                parts.Add(e == selected ? $"[{text}]" : text);
            }
            return "Pocket: " + string.Join("  ", parts);
        }

        private Entry Find(string id)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id) return _entries[i];
            }
            return null;
        }

        private void ClampSelection()
        {
            if (_entries.Count == 0) _selected = 0;
            else _selected = Mathf.Clamp(_selected, 0, _entries.Count - 1);
        }
    }
}
