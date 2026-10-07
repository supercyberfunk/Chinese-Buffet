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
        private const float StatusPollSeconds = 0.25f;

        private static readonly Color GainColor = new Color(0.35f, 1f, 0.45f);
        private static readonly Color LossColor = new Color(1f, 0.4f, 0.35f);
        private static readonly Color WarmColor = new Color(1f, 0.72f, 0.25f);
        private static readonly Color DimColor = new Color(0.75f, 0.75f, 0.8f);
        private static readonly Color WalletColor = new Color(0.8f, 0.9f, 1f);

        private struct Outcome
        {
            public string Text;
            public Color Color;
            public float Until;
        }

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
        private Text _walletText;
        private Text _pocketText;
        private Text _effectsText;
        private PlayerInventory _inventory;
        private PlayerPocket _pocket;
        private PlayerEffects _effects;
        private readonly List<ChaosEventInfo> _activeEvents = new List<ChaosEventInfo>();
        private readonly List<Outcome> _outcomes = new List<Outcome>();
        private float _walletFlash;
        private Color _walletFlashColor = Color.white;
        private float _statusTimer;
        private readonly List<string> _log = new List<string>();
        private float _balanceFlash;
        private Color _balanceFlashColor = Color.white;
        private float _satisfactionFlash;
        private Color _satisfactionFlashColor = Color.white;
        private string _satisfactionBase = "Satisfaction --";
        private Text _ticketText;
        private bool _phoneRinging;
        private bool _hasOrder;
        private ToGoOrderInfo _order;
        private string _ticketOutcome = string.Empty;
        private float _ticketOutcomeUntil;

        public void Initialize(Font font, PlayerInventory inventory, PlayerPocket pocket = null, PlayerEffects effects = null)
        {
            _font = font;
            _inventory = inventory;
            _pocket = pocket;
            _effects = effects;
            if (_inventory != null) _inventory.Changed += RefreshCarry;
            if (_pocket != null) _pocket.Changed += RefreshPocket;
            BuildCanvas();
            RefreshCarry();
            RefreshPocket();
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
            GameEvents.WalletChanged += OnWalletChanged;
            GameEvents.WalletCredited += OnWalletCredited;
            GameEvents.PhoneRingingChanged += OnPhoneRingingChanged;
            GameEvents.ToGoOrderPlaced += OnToGoOrderPlaced;
            GameEvents.ToGoOrderTicked += OnToGoOrderTicked;
            GameEvents.ToGoOrderEnded += OnToGoOrderEnded;
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
            GameEvents.WalletChanged -= OnWalletChanged;
            GameEvents.WalletCredited -= OnWalletCredited;
            GameEvents.PhoneRingingChanged -= OnPhoneRingingChanged;
            GameEvents.ToGoOrderPlaced -= OnToGoOrderPlaced;
            GameEvents.ToGoOrderTicked -= OnToGoOrderTicked;
            GameEvents.ToGoOrderEnded -= OnToGoOrderEnded;
            if (_inventory != null) _inventory.Changed -= RefreshCarry;
            if (_pocket != null) _pocket.Changed -= RefreshPocket;
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

            if (_walletText != null && _walletFlash > 0f)
            {
                _walletFlash -= dt;
                _walletText.color = Color.Lerp(WalletColor, _walletFlashColor, Mathf.Clamp01(_walletFlash));
            }

            if (_outcomes.Count > 0 && FirstOutcomeExpired())
            {
                _outcomes.RemoveAll(o => o.Until <= Time.time);
                RefreshEventBanner();
            }

            if (!string.IsNullOrEmpty(_ticketOutcome) && Time.time >= _ticketOutcomeUntil)
            {
                _ticketOutcome = string.Empty;
                RefreshTicket();
            }

            _statusTimer -= dt;
            if (_statusTimer <= 0f)
            {
                _statusTimer = StatusPollSeconds;
                if (_effectsText != null) _effectsText.text = _effects != null ? _effects.StatusLine : string.Empty;
            }
        }

        private bool FirstOutcomeExpired()
        {
            for (int i = 0; i < _outcomes.Count; i++)
            {
                if (_outcomes[i].Until <= Time.time) return true;
            }
            return false;
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
            _carryText = MakeText(root, "Carry", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -186f), new Vector2(900f, 40f), 24, TextAnchor.UpperLeft, FontStyle.Bold);
            _pocketText = MakeText(root, "Pocket", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -222f), new Vector2(900f, 34f), 22, TextAnchor.UpperLeft, FontStyle.Normal);
            _effectsText = MakeText(root, "Effects", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -256f), new Vector2(1000f, 34f), 22, TextAnchor.UpperLeft, FontStyle.Bold);
            _effectsText.color = WarmColor;
            _walletText = MakeText(root, "Wallet", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(380f, -30f), new Vector2(420f, 50f), 30, TextAnchor.UpperLeft, FontStyle.Bold);
            _walletText.color = WalletColor;
            _dayText = MakeText(root, "Day", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 60f), 36, TextAnchor.MiddleCenter, FontStyle.Bold);
            _eventText = MakeText(root, "Event", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 130f), 28, TextAnchor.UpperCenter, FontStyle.Bold);
            _promptText = MakeText(root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1200f, 60f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            _logText = MakeText(root, "Log", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(1100f, 220f), 20, TextAnchor.LowerLeft, FontStyle.Normal);
            Text help = MakeText(root, "Help", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(760f, 220f), 19, TextAnchor.UpperRight, FontStyle.Normal);
            help.text = "WASD move  |  Mouse look  |  Shift sprint  |  Space jump\nE interact (hold E when the prompt shows a bar)  |  Q drop / cancel\nLeft click throw or use pocket item  |  Tab next item  |  R crack a cookie\nEsc free cursor (click to re-lock)  |  F1 debug keys\n\nLoop: buy raw boxes at the cooler (back), cook them in the fryer, wok,\nsteamer or rice cooker, refill trays, clear plates, load the dishwasher.\nRed !! over a customer: they're about to run. Tackle with E.";
            help.color = new Color(1f, 1f, 1f, 0.8f);
            _ticketText = MakeText(root, "Phone Ticket", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -250f), new Vector2(640f, 260f), 22, TextAnchor.UpperRight, FontStyle.Normal);
            _ticketText.text = string.Empty;
            Text crosshair = MakeText(root, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            crosshair.text = "+";
            crosshair.color = new Color(1f, 1f, 1f, 0.7f);

            BuildSummaryPanel(root);

            _balanceText.text = "$0.00";
            _walletText.text = string.Empty;
            _pocketText.text = string.Empty;
            _effectsText.text = string.Empty;
            _statsText.text = string.Empty;
            _satisfactionText.text = _satisfactionBase;
            _dayText.text = string.Empty;
            _eventText.text = string.Empty;
            _eventText.color = Color.white;
            _promptText.text = string.Empty;
            _logText.text = string.Empty;
        }

        /// <summary>A centred dark panel with one text block; hidden until the ledger publishes a day summary.</summary>
        private void BuildSummaryPanel(Transform root)
        {
            _summaryPanel = new GameObject("Day Summary");
            _summaryPanel.transform.SetParent(root, false);
            RectTransform rect = _summaryPanel.AddComponent<RectTransform>();
            // Right of centre, so the fountain (and the scramble for it at close) stays in view.
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-40f, 0f);
            rect.sizeDelta = new Vector2(760f, 640f);
            Image background = _summaryPanel.AddComponent<Image>();
            background.color = new Color(0.02f, 0.02f, 0.05f, 0.82f);
            background.raycastTarget = false;

            _summaryText = MakeText(_summaryPanel.transform, "Summary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 600f), 24, TextAnchor.MiddleCenter, FontStyle.Normal);
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

        private void RefreshPocket()
        {
            if (_pocketText == null) return;
            _pocketText.text = _pocket != null ? _pocket.Describe() : string.Empty;
        }

        private void OnWalletChanged(float total, float delta, string reason)
        {
            if (_walletText == null) return;
            _walletText.text = $"Wallet ${total:0.00}";
            if (Mathf.Abs(delta) < 0.005f) return;
            _walletFlashColor = delta > 0f ? GainColor : LossColor;
            _walletFlash = 1f;
        }

        /// <summary>Quarters and payouts for the player alone float up in silver, apart from the till's gold.</summary>
        private void OnWalletCredited(float amount, string reason, Vector3 at)
        {
            if (amount <= 0f || at == Vector3.zero) return;
            FloatingText.Spawn(at + Vector3.up * 1.6f, $"+${amount:0.00} (you)", WalletColor, _font);
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
            // The scheduler aborts any running event at close without a final outcome, so drop its banner here.
            if (snapshot.Phase == DayPhase.Closed)
            {
                _activeEvents.Clear();
                RefreshEventBanner();
            }
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
            // Stays up until the event ends; several can run at once (fortunes and robberies stack on the day's events).
            _activeEvents.Add(info);
            RefreshEventBanner();
        }

        private void OnChaosEventEnded(ChaosEventInfo info, bool resolved, string outcome)
        {
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (_activeEvents[i].Id != info.Id) continue;
                _activeEvents.RemoveAt(i);
                break;
            }
            _outcomes.Add(new Outcome
            {
                Text = $"<size=34>{info.DisplayName}</size>\n{outcome}",
                Color = resolved ? GainColor : LossColor,
                Until = Time.time + EventOutcomeSeconds,
            });
            RefreshEventBanner();
        }

        // ----- Phone orders -----

        private void OnPhoneRingingChanged(bool ringing)
        {
            _phoneRinging = ringing;
            RefreshTicket();
        }

        private void OnToGoOrderPlaced(ToGoOrderInfo order)
        {
            _order = order;
            _hasOrder = true;
            _ticketOutcome = string.Empty;
            RefreshTicket();
        }

        private void OnToGoOrderTicked(ToGoOrderInfo order)
        {
            _order = order;
            _hasOrder = true;
            RefreshTicket();
        }

        private void OnToGoOrderEnded(ToGoOrderInfo order, bool delivered, string outcome)
        {
            _hasOrder = false;
            if (outcome == "closed") _ticketOutcome = string.Empty;
            else
            {
                _ticketOutcome = delivered
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(GainColor)}>{order.CallerName}'s order picked up{(outcome == "delivered short" ? " (short)" : "")}.</color>"
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(LossColor)}>{order.CallerName}'s order expired.</color>";
                _ticketOutcomeUntil = Time.time + 6f;
            }
            RefreshTicket();
        }

        /// <summary>Top right, under the help: the ring, then the ticket. The caller's words only stay while they talk.</summary>
        private void RefreshTicket()
        {
            if (_ticketText == null) return;
            string warm = ColorUtility.ToHtmlStringRGB(WarmColor);
            if (_phoneRinging)
            {
                _ticketText.text = $"<color=#{warm}><size=32>PHONE RINGING</size></color>\nAnswer it at the front counter (E)";
                return;
            }
            if (_hasOrder)
            {
                string words = _order.OnTheLine
                    ? $"<color=#{warm}>{_order.Script}</color>"
                    : "<color=#AAAAAA>(they hung up; it's from memory now)</color>";
                string urgency = _order.SecondsLeft <= 30f ? ColorUtility.ToHtmlStringRGB(LossColor) : "FFFFFF";
                _ticketText.text = $"<size=26>TO-GO #{_order.Id} for {_order.CallerName}</size>\n{words}\n<color=#{urgency}>{Mathf.CeilToInt(_order.SecondsLeft)}s</color> to rack a box on the pickup shelf by the door ({_order.TotalUnits} units, {_order.ItemCount} items)";
                return;
            }
            _ticketText.text = _ticketOutcome;
        }

        private void RefreshEventBanner()
        {
            if (_eventText == null) return;
            var banner = new System.Text.StringBuilder();
            for (int i = 0; i < _activeEvents.Count; i++)
            {
                if (banner.Length > 0) banner.Append('\n');
                banner.Append($"<color=#{ColorUtility.ToHtmlStringRGB(WarmColor)}><size={(_activeEvents.Count > 1 ? 34 : 44)}>{_activeEvents[i].DisplayName}</size>\n{_activeEvents[i].Description}</color>");
            }
            for (int i = 0; i < _outcomes.Count; i++)
            {
                if (banner.Length > 0) banner.Append('\n');
                banner.Append($"<color=#{ColorUtility.ToHtmlStringRGB(_outcomes[i].Color)}>{_outcomes[i].Text}</color>");
            }
            _eventText.text = banner.ToString();
            _eventText.color = Color.white;
        }
    }
}
