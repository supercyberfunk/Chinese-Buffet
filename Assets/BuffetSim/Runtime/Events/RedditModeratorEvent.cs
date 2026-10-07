using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A seated customer turns into a reddit moderator: trench coat, fedora, and a running commentary
    /// on the authenticity of the food, in the wrong language. Satisfaction drains while he talks.
    /// Knock him out (E, or a rock): his bill comes out doubled, as coins. Then throw him out (E
    /// again) for the satisfaction bump. Ignore him and he eventually pays normally and leaves.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Reddit Moderator", fileName = "RedditModerator")]
    public sealed class RedditModeratorEvent : ChaosEvent
    {
        [SerializeField] private float billMultiplier = 2f;
        [SerializeField] private float talkInterval = 10f;
        [SerializeField] private float reputationPerLine = -2f;
        [SerializeField] private float reputationForEviction = 6f;
        [SerializeField] private float patienceSeconds = 100f;

        public float BillMultiplier => billMultiplier;
        public float TalkInterval => talkInterval;
        public float ReputationPerLine => reputationPerLine;
        public float ReputationForEviction => reputationForEviction;
        public float PatienceSeconds => patienceSeconds;

        public static RedditModeratorEvent CreateDefault()
        {
            var e = CreateInstance<RedditModeratorEvent>();
            e.Configure("RedditModerator", "Reddit moderator", "Someone at a table has started explaining authenticity.", 1f, 1, 240f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<RedditModeratorRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="RedditModeratorEvent"/>; also the E key and the rock on the moderator while it lasts.</summary>
    public sealed class RedditModeratorRunner : ChaosEventRunner, IInteractable, IThrowTarget
    {
        private static readonly Color CoatColor = new Color(0.35f, 0.3f, 0.22f);
        private static readonly string[] Lines =
        {
            "\"Actually, this is more of a chashu than a char siu. Itadakimasu.\"",
            "\"Real lo mein wouldn't use this noodle. Source: I moderate r/noodles.\"",
            "\"Sumimasen? The crab rangoon is American. I'm just saying what everyone's thinking.\"",
            "\"I've locked this table. Too many off-topic comments.\"",
            "\"Oishii. Though technically... (he keeps going)\"",
            "\"This Kung Pao has been reported for being inauthentic. Mods have been notified. I am the mods.\"",
            "\"Arigatou. Anyway, MSG is a myth, but also this has too much of it.\"",
        };

        private RedditModeratorEvent _event;
        private CustomerAgent _mod;
        private float _talkTimer;
        private float _patience;
        private bool _knockedOut;
        private bool _evicted;

        protected override void OnBegin()
        {
            _event = Definition as RedditModeratorEvent;
            _mod = ChaosActors.PickSeatedCustomer(Ctx);
            if (_mod == null)
            {
                GameEvents.RaiseNotice("Someone was about to explain authenticity, but nobody was sitting down, so they typed it instead.");
                Finish(true, "No audience");
                return;
            }

            _mod.Restyle(CoatColor, " (mod)");
            _mod.AddAccessory("Trench Coat", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.5f, 0.75f), CoatColor);
            _mod.AddAccessory("Fedora Brim", PrimitiveType.Cylinder, new Vector3(0f, 2.12f, 0f), new Vector3(0.6f, 0.02f, 0.6f), new Color(0.15f, 0.12f, 0.1f));
            _mod.AddAccessory("Fedora", PrimitiveType.Cylinder, new Vector3(0f, 2.22f, 0f), new Vector3(0.4f, 0.1f, 0.4f), new Color(0.15f, 0.12f, 0.1f));
            _mod.Hold("Explaining authenticity");
            _mod.InteractOverride = this;
            _talkTimer = 1.5f;
            _patience = _event != null ? _event.PatienceSeconds : 100f;
            GameEvents.RaiseNotice($"{_mod.CustomerName} has put on a trench coat and a fedora and is explaining the food to the room. (audio cue: tipped hat)");
        }

        private void Update()
        {
            if (IsFinished) return;
            if (_mod == null)
            {
                Finish(true, "The moderator is gone");
                return;
            }
            if (_knockedOut) return;

            float dt = Time.deltaTime;
            _talkTimer -= dt;
            if (_talkTimer <= 0f)
            {
                _talkTimer = _event != null ? Mathf.Max(2f, _event.TalkInterval) : 10f;
                GameEvents.RaiseNotice($"{_mod.CustomerName}: {ChaosActors.Pick(Ctx.Rng, Lines)}");
                GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationPerLine : -2f, "the moderator");
            }

            _patience -= dt;
            if (_patience <= 0f)
            {
                // He finishes his meal like anyone else; the coat stays on.
                _mod.InteractOverride = null;
                _mod.ReleaseHold();
                GameEvents.RaiseNotice($"{_mod.CustomerName} has finished explaining. He'll pay like a normal person, unfortunately.");
                Finish(false, "The moderator paid and left on his own terms");
            }
        }

        private void KnockOut(string how)
        {
            if (IsFinished || _knockedOut || _mod == null) return;
            _knockedOut = true;
            float multiplier = _event != null ? _event.BillMultiplier : 2f;
            float amount = _mod.SettleAsCoins(multiplier, "the moderator's bill, doubled");
            _mod.KnockOut(Ctx.Config != null ? Ctx.Config.KnockoutSeconds : 25f);
            GameEvents.RaiseNotice(amount > 0f
                ? $"{how} The room applauds. ${amount:0.00} ({multiplier:0.#}x the bill) fell out of the trench coat. Throw him out (E)."
                : $"{how} He hadn't eaten anything worth charging for. Throw him out (E).");
        }

        private void Evict()
        {
            if (IsFinished || _evicted || _mod == null) return;
            _evicted = true;
            string name = _mod.CustomerName;
            GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationForEviction : 6f, "the moderator was thrown out");
            GameEvents.RaiseNotice($"You dragged {name} out to the sidewalk by the coat. The fedora rolled after him.");
            _mod.Vanish();
            _mod = null;
            Finish(true, "Moderator knocked out and thrown out, bill doubled");
        }

        // ----- The E key and thrown things, while the event owns the customer -----

        public string GetPrompt(PlayerInventory inventory)
        {
            if (_mod == null || IsFinished) return string.Empty;
            return _knockedOut ? "[E] Throw the moderator out" : "[E] Knock out the moderator";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_knockedOut) Evict();
            else KnockOut("You decked the moderator mid-sentence.");
        }

        public bool AcceptsThrow(ThrowableKind kind) => !IsFinished && _mod != null && !_knockedOut && kind != ThrowableKind.Cookie;
        public int HomingPriority => !IsFinished && _mod != null && !_knockedOut ? 2 : 0;
        public Vector3 AimPoint => _mod != null ? _mod.transform.position + Vector3.up * 1.3f : transform.position;

        public void OnThrowHit(ThrowableKind kind)
        {
            if (kind == ThrowableKind.Rock) KnockOut("The rock hit the moderator square in the fedora.");
            else if (kind == ThrowableKind.Dodgeball) GameEvents.RaiseNotice("Bonk. The moderator paused, then continued from where he left off.");
        }

        public override void Abort()
        {
            if (_mod != null)
            {
                _mod.InteractOverride = null;
                _mod.ReleaseHold();
            }
            EndSilently();
        }
    }
}
