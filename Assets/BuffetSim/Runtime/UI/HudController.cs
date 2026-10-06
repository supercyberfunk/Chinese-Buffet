using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BuffetSim.UI
{
    /// <summary>
    /// Builds a screen-space HUD at runtime (balance, day stats, interaction prompt, event log)
    /// and spawns world-space money popups. Pure presentation: it only listens to the event bus.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private const int MaxLogLines = 7;

        private Font _font;
        private Text _balanceText;
        private Text _statsText;
        private Text _promptText;
        private Text _logText;
        private Text _carryText;
        private PlayerInventory _inventory;
        private readonly List<string> _log = new List<string>();
        private float _balanceFlash;
        private Color _balanceFlashColor = Color.white;

        public void Initialize(Font font, PlayerInventory inventory)
        {
            _font = font;
            _inventory = inventory;
            if (_inventory != null) _inventory.Changed += RefreshCarry;
            BuildCanvas();
            RefreshCarry();
        }

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.LedgerUpdated += OnLedgerUpdated;
            GameEvents.Notice += OnNotice;
            GameEvents.PromptChanged += OnPromptChanged;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.LedgerUpdated -= OnLedgerUpdated;
            GameEvents.Notice -= OnNotice;
            GameEvents.PromptChanged -= OnPromptChanged;
            if (_inventory != null) _inventory.Changed -= RefreshCarry;
        }

        private void Update()
        {
            if (_balanceText == null) return;
            if (_balanceFlash > 0f)
            {
                _balanceFlash -= Time.deltaTime;
                _balanceText.color = Color.Lerp(Color.white, _balanceFlashColor, Mathf.Clamp01(_balanceFlash));
            }
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Transform root = canvasGo.transform;
            _balanceText = MakeText(root, "Balance", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(700f, 70f), 48, TextAnchor.UpperLeft, FontStyle.Bold);
            _statsText = MakeText(root, "Stats", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(900f, 90f), 22, TextAnchor.UpperLeft, FontStyle.Normal);
            _carryText = MakeText(root, "Carry", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -170f), new Vector2(700f, 40f), 24, TextAnchor.UpperLeft, FontStyle.Bold);
            _promptText = MakeText(root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1200f, 60f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            _logText = MakeText(root, "Log", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(1100f, 220f), 20, TextAnchor.LowerLeft, FontStyle.Normal);
            Text help = MakeText(root, "Help", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(560f, 200f), 20, TextAnchor.UpperRight, FontStyle.Normal);
            help.text = "WASD move  |  Mouse look  |  Shift sprint  |  Space jump\nE interact  |  Q drop  |  Esc free cursor (click to re-lock)\n\nLoop: buy food at the cooler (back), refill trays,\nclear plates from tables, load the dishwasher.";
            help.color = new Color(1f, 1f, 1f, 0.8f);
            Text crosshair = MakeText(root, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            crosshair.text = "+";
            crosshair.color = new Color(1f, 1f, 1f, 0.7f);

            _balanceText.text = "$0.00";
            _statsText.text = string.Empty;
            _promptText.text = string.Empty;
            _logText.text = string.Empty;
        }

        private Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private void OnMoneyChanged(MoneyChange change)
        {
            if (_balanceText != null) _balanceText.text = $"${change.Balance:0.00}";

            if (Mathf.Abs(change.Delta) < 0.005f) return;
            bool gain = change.Delta > 0f;
            _balanceFlashColor = gain ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.4f, 0.35f);
            _balanceFlash = 1f;

            if (change.HasWorldPosition)
            {
                string sign = gain ? "+" : "-";
                FloatingText.Spawn(change.WorldPosition + Vector3.up * 2.1f, $"{sign}${Mathf.Abs(change.Delta):0.00}", _balanceFlashColor, _font);
            }
        }

        private void OnLedgerUpdated(LedgerSnapshot snapshot)
        {
            if (_statsText == null) return;
            _statsText.text =
                $"Today: {snapshot.CustomersServed} served, {snapshot.CustomersLost} walked out\n" +
                $"Revenue +${snapshot.RevenueToday:0.00}   Food & penalties -${snapshot.ExpensesToday:0.00}   Lost to missing units -${snapshot.DeductionsToday:0.00}";
        }

        private void OnNotice(string message)
        {
            _log.Add(message);
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
            if (_logText != null) _logText.text = string.Join("\n", _log);
        }

        private void OnPromptChanged(string prompt)
        {
            if (_promptText != null) _promptText.text = prompt;
        }

        private void RefreshCarry()
        {
            if (_carryText == null || _inventory == null) return;
            _carryText.text = _inventory.Describe();
        }
    }
}
