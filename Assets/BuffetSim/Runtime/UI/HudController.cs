using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BuffetSim.UI
{
    /// <summary>
    /// Builds a screen-space HUD at runtime (balance, day stats, satisfaction, day clock, chaos
    /// event banner, end-of-day summary, interaction prompt, event log) and spawns world-space
    /// money popups. Pure presentation: it only listens to the event bus and never calls a system.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private const int MaxLogLines = 7;
        private const float SatisfactionReasonSeconds = 3f;
        private const float EventOutcomeSeconds = 5f;

        private static readonly Color GainColor = new Color(0.35f, 1f, 0.45f);
        private static readonly Color LossColor = new Color(1f, 0.4f, 0.35f);
        private static readonly Color WarmColor = new Color(1f, 0.72f, 0.25f);
        private static readonly Color DimColor = new Color(0.75f, 0.75f, 0.8f);

        private Font _font;
        private Text _balanceText;
        private Text _statsText;
        private Text _satisfactionText;
        private Text _promptText;
        private Text _logText;
        private Text _carryText;
        private Text _dayText;
        private Text _eventText;
        private GameObject _summaryPanel;
        private Text _summaryText;
        private PlayerInventory _inventory;
        private readonly List<string> _log = new List<string>();
        private float _balanceFlash;
        private Color _balanceFlashColor = Color.white;
        private float _satisfactionFlash;
        private Color _satisfactionFlashColor = Color.white;
        private string _satisfactionBase = "Satisfaction --";
        private float _eventTimer;

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
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayClockTicked += OnDayClockTicked;
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
            GameEvents.DaySummaryReady += OnDaySummaryReady;
            GameEvents.ReputationChanged += OnReputationChanged;
            GameEvents.ChaosEventStarted += OnChaosEventStarted;
            GameEvents.ChaosEventEnded += OnChaosEventEnded;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.LedgerUpdated -= OnLedgerUpdated;
            GameEvents.Notice -= OnNotice;
            GameEvents.PromptChanged -= OnPromptChanged;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayClockTicked -= OnDayClockTicked;
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
            GameEvents.DaySummaryReady -= OnDaySummaryReady;
            GameEvents.ReputationChanged -= OnReputationChanged;
            GameEvents.ChaosEventStarted -= OnChaosEventStarted;
            GameEvents.ChaosEventEnded -= OnChaosEventEnded;
            if (_inventory != null) _inventory.Changed -= RefreshCarry;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_balanceText != null && _balanceFlash > 0f)
            {
                _balanceFlash -= dt;
                _balanceText.color = Color.Lerp(Color.white, _balanceFlashColor, Mathf.Clamp01(_balanceFlash));
            }

            if (_satisfactionText != null && _satisfactionFlash > 0f)
            {
                _satisfactionFlash -= dt;
                _satisfactionText.color = Color.Lerp(Color.white, _satisfactionFlashColor, Mathf.Clamp01(_satisfactionFlash / SatisfactionReasonSeconds));
                if (_satisfactionFlash <= 0f)
                {
                    _satisfactionText.text = _satisfactionBase;
                    _satisfactionText.color = Color.white;
                }
            }

            if (_eventText != null && _eventTimer > 0f)
            {
                _eventTimer -= dt;
                if (_eventTimer <= 0f) _eventText.text = string.Empty;
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
            _statsText = MakeText(root, "Stats", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(900f, 60f), 22, TextAnchor.UpperLeft, FontStyle.Normal);
            _satisfactionText = MakeText(root, "Satisfaction", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -148f), new Vector2(900f, 34f), 22, TextAnchor.UpperLeft, FontStyle.Bold);
            _carryText = MakeText(root, "Carry", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -186f), new Vector2(700f, 40f), 24, TextAnchor.UpperLeft, FontStyle.Bold);
            _dayText = MakeText(root, "Day", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 60f), 36, TextAnchor.MiddleCenter, FontStyle.Bold);
            _eventText = MakeText(root, "Event", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 130f), 28, TextAnchor.UpperCenter, FontStyle.Bold);
            _promptText = MakeText(root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1200f, 60f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            _logText = MakeText(root, "Log", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(1100f, 220f), 20, TextAnchor.LowerLeft, FontStyle.Normal);
            Text help = MakeText(root, "Help", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(560f, 200f), 20, TextAnchor.UpperRight, FontStyle.Normal);
            help.text = "WASD move  |  Mouse look  |  Shift sprint  |  Space jump\nE interact  |  Q drop  |  Esc free cursor (click to re-lock)\n\nLoop: buy food at the cooler (back), refill trays,\nclear plates from tables, load the dishwasher.";
            help.color = new Color(1f, 1f, 1f, 0.8f);
            Text crosshair = MakeText(root, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            crosshair.text = "+";
            crosshair.color = new Color(1f, 1f, 1f, 0.7f);

            BuildSummaryPanel(root);

            _balanceText.text = "$0.00";
            _statsText.text = string.Empty;
            _satisfactionText.text = _satisfactionBase;
            _dayText.text = string.Empty;
            _eventText.text = string.Empty;
            _eventText.color = WarmColor;
            _promptText.text = string.Empty;
            _logText.text = string.Empty;
        }

        /// <summary>A centred dark panel with one text block; hidden until the ledger publishes a day summary.</summary>
        private void BuildSummaryPanel(Transform root)
        {
            _summaryPanel = new GameObject("Day Summary");
            _summaryPanel.transform.SetParent(root, false);
            RectTransform rect = _summaryPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(820f, 600f);
            Image background = _summaryPanel.AddComponent<Image>();
            background.color = new Color(0.02f, 0.02f, 0.05f, 0.82f);
            background.raycastTarget = false;

            _summaryText = MakeText(_summaryPanel.transform, "Summary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f), 26, TextAnchor.MiddleCenter, FontStyle.Normal);
            _summaryText.text = string.Empty;
            _summaryPanel.SetActive(false);
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
            text.supportRichText = true;
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
            _balanceFlashColor = gain ? GainColor : LossColor;
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
                $"Today: {snapshot.CustomersServed} served, {snapshot.CustomersLost} walked out, {snapshot.DineAndDashesToday} dashed ({snapshot.DashersCaughtToday} caught)   Your cut: ${snapshot.PlayerCash:0.00}\n" +
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

        private void OnDayStarted(int day)
        {
            if (_summaryPanel != null) _summaryPanel.SetActive(false);
        }

        private void OnDayClockTicked(DayClockSnapshot snapshot)
        {
            RefreshDayLine(snapshot);
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            RefreshDayLine(snapshot);
        }

        private void RefreshDayLine(DayClockSnapshot snapshot)
        {
            if (_dayText == null) return;
            string clock;
            Color color;
            switch (snapshot.Phase)
            {
                case DayPhase.LastCall:
                    clock = $"LAST CALL {FormatClock(snapshot.SecondsRemaining)}";
                    color = WarmColor;
                    break;
                case DayPhase.Closing:
                    clock = "CLOSING";
                    color = WarmColor;
                    break;
                case DayPhase.Closed:
                    clock = "CLOSED";
                    color = DimColor;
                    break;
                default:
                    clock = $"{FormatClock(snapshot.SecondsRemaining)} left";
                    color = Color.white;
                    break;
            }
            _dayText.text = $"Day {snapshot.Day}   {clock}";
            _dayText.color = color;
        }

        private static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private void OnDaySummaryReady(DaySummary summary)
        {
            if (_summaryPanel == null || _summaryText == null) return;

            string split;
            if (summary.Profit > 0f)
            {
                float storePercent = summary.StoreShare / summary.Profit * 100f;
                float playerPercent = summary.PlayerShare / summary.Profit * 100f;
                split = $"Store keeps {storePercent:0}%:  ${summary.StoreShare:0.00}\n" +
                        $"Your cut ({playerPercent:0}%):  ${summary.PlayerShare:0.00}";
            }
            else
            {
                split = "No profit today, so no payout.";
            }

            string profitSign = summary.Profit < 0f ? "-" : string.Empty;
            _summaryText.text =
                $"<b><size=36>DAY {summary.Day} SUMMARY</size></b>\n\n" +
                $"Customers served: {summary.CustomersServed}    Walked out: {summary.CustomersLost}\n" +
                $"Dine and dash: {summary.DineAndDashes}    Caught: {summary.DashersCaught}\n\n" +
                $"Revenue  +${summary.Revenue:0.00}\n" +
                $"Expenses  -${summary.Expenses:0.00}\n" +
                $"Lost to missing units  -${summary.Deductions:0.00}\n" +
                $"<b>Profit  {profitSign}${Mathf.Abs(summary.Profit):0.00}</b>\n\n" +
                $"{split}\n" +
                $"Your cash total:  ${summary.PlayerCashTotal:0.00}\n\n" +
                $"Satisfaction: {Mathf.RoundToInt(summary.Reputation)}%\n\n" +
                "<i>Lights off. The next day opens shortly.</i>";
            _summaryPanel.SetActive(true);
        }

        private void OnReputationChanged(float value, float delta, string reason)
        {
            if (_satisfactionText == null) return;
            _satisfactionBase = $"Satisfaction {Mathf.RoundToInt(value)}%";

            if (Mathf.Abs(delta) < 0.005f)
            {
                _satisfactionFlash = 0f;
                _satisfactionText.text = _satisfactionBase;
                _satisfactionText.color = Color.white;
                return;
            }

            bool gain = delta > 0f;
            _satisfactionFlashColor = gain ? GainColor : LossColor;
            _satisfactionFlash = SatisfactionReasonSeconds;
            string sign = gain ? "+" : "-";
            string change = $"{sign}{Mathf.Abs(delta):0.#}";
            _satisfactionText.text = string.IsNullOrEmpty(reason)
                ? $"{_satisfactionBase}   ({change})"
                : $"{_satisfactionBase}   ({change}: {reason})";
            _satisfactionText.color = _satisfactionFlashColor;
        }

        private void OnChaosEventStarted(ChaosEventInfo info)
        {
            if (_eventText == null) return;
            _eventText.text = $"<size=44>{info.DisplayName}</size>\n{info.Description}";
            _eventText.color = WarmColor;
            _eventTimer = 0f; // stays up until the event ends
        }

        private void OnChaosEventEnded(ChaosEventInfo info, bool resolved, string outcome)
        {
            if (_eventText == null) return;
            _eventText.text = $"<size=44>{info.DisplayName}</size>\n{outcome}";
            _eventText.color = resolved ? GainColor : LossColor;
            _eventTimer = EventOutcomeSeconds;
        }
    }
}
