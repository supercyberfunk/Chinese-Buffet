using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Events;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Debugging
{
    /// <summary>
    /// Playtest keys on F1-F12, with an IMGUI panel listing them. Everything it does goes through
    /// the bus (or the player's own pocket), so it can be deleted without touching a system.
    /// </summary>
    public sealed class DebugTools : MonoBehaviour
    {
        private readonly List<string> _eventIds = new List<string>();
        private readonly List<string> _eventNames = new List<string>();
        private PlayerPocket _pocket;
        private Transform _player;
        private int _selected;
        private bool _panelOpen;
        private string _lastAction = string.Empty;
        private float _lastActionAt = float.NegativeInfinity;

        public void Initialize(ChaosEventCatalog catalog, PlayerPocket pocket, Transform player)
        {
            _pocket = pocket;
            _player = player;
            _eventIds.Clear();
            _eventNames.Clear();
            if (catalog == null) return;
            for (int i = 0; i < catalog.Events.Count; i++)
            {
                ChaosEvent e = catalog.Events[i];
                if (e == null) continue;
                _eventIds.Add(e.name);
                _eventNames.Add(e.DisplayName);
            }
        }

        private void Update()
        {
            if (InputReader.FunctionKeyPressed(1)) _panelOpen = !_panelOpen;
            if (InputReader.FunctionKeyPressed(2)) SelectEvent(-1);
            if (InputReader.FunctionKeyPressed(3)) SelectEvent(1);
            if (InputReader.FunctionKeyPressed(4)) StartSelectedEvent();
            if (InputReader.FunctionKeyPressed(5)) Do("Rang the phone", GameEvents.RaisePhoneRingRequested);
            if (InputReader.FunctionKeyPressed(6)) Do("+$100 in the till", () => GameEvents.RaiseMoneyRecovered(100f, "debug money", PlayerPosition()));
            if (InputReader.FunctionKeyPressed(7)) Do("+$20 in your wallet", () => GameEvents.RaiseWalletCredited(20f, "debug money", Vector3.zero));
            if (InputReader.FunctionKeyPressed(8)) Do("Skipped 60 s", () => GameEvents.RaiseDayFastForwardRequested(60f));
            if (InputReader.FunctionKeyPressed(9)) Do("Spawned a customer", () => GameEvents.RaiseCustomerSpawnRequested(1));
            if (InputReader.FunctionKeyPressed(10)) GiveCookie();
            if (InputReader.FunctionKeyPressed(11)) GiveThrowables();
            if (InputReader.FunctionKeyPressed(12)) Do("Robbery!", () => GameEvents.RaiseChaosEventRequested(RobberyScheduler.EventId));
        }

        private void SelectEvent(int direction)
        {
            if (_eventIds.Count == 0) return;
            _selected = (_selected + direction + _eventIds.Count) % _eventIds.Count;
            _panelOpen = true;
            Flash($"Selected: {_eventNames[_selected]}");
        }

        private void StartSelectedEvent()
        {
            if (_eventIds.Count == 0) return;
            string id = _eventIds[_selected];
            Do($"Started {_eventNames[_selected]}", () => GameEvents.RaiseChaosEventRequested(id));
        }

        private void GiveCookie()
        {
            if (_pocket == null) return;
            int added = _pocket.Add(PocketItems.Cookie, "fortune cookie", 1, PlayerThrower.CookieMax);
            Flash(added > 0 ? "A fortune cookie appeared in your pocket" : "Pocket full of cookies");
        }

        private void GiveThrowables()
        {
            if (_pocket == null) return;
            _pocket.Add(PocketItems.Rock, "rock", 1, PlayerThrower.RockMax);
            _pocket.Add(PocketItems.Dodgeball, "dodgeball", 3, PlayerThrower.DodgeballMax);
            Flash("A rock and three dodgeballs appeared in your pocket");
        }

        private void Do(string what, System.Action action)
        {
            action?.Invoke();
            Flash(what);
        }

        private void Flash(string what)
        {
            _lastAction = what;
            _lastActionAt = Time.time;
        }

        private Vector3 PlayerPosition()
        {
            return _player != null ? _player.position : Vector3.zero;
        }

        private void OnGUI()
        {
            if (!_panelOpen)
            {
                if (Time.time - _lastActionAt < 2f) GUI.Label(new Rect(Screen.width * 0.5f - 200f, 8f, 400f, 24f), $"[debug] {_lastAction}");
                return;
            }

            string selected = _eventIds.Count > 0 ? _eventNames[_selected] : "(no catalog)";
            string text =
                "DEBUG (F1 closes)\n" +
                $"F2 / F3  select event: < {selected} >\n" +
                "F4  start the selected event\n" +
                "F5  ring the phone\n" +
                "F6  +$100 in the till\n" +
                "F7  +$20 in your wallet\n" +
                "F8  skip 60 seconds\n" +
                "F9  spawn a customer\n" +
                "F10 a fortune cookie in your pocket\n" +
                "F11 a rock and three dodgeballs\n" +
                "F12 robbery\n" +
                (Time.time - _lastActionAt < 2f ? $"\n> {_lastAction}" : string.Empty);
            GUI.Box(new Rect(Screen.width - 340f, Screen.height * 0.5f - 140f, 330f, 280f), text);
        }
    }
}
