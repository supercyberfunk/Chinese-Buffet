using BuffetSim.Bootstrap;
using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// Sci-fi sounds, a saucer over the dining room, and the player goes up the beam. Out of the game
    /// for the knockout time, then dropped at the front door. Nothing to do but lose what you carried.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Alien Abduction", fileName = "AlienAbduction")]
    public sealed class AlienAbductionEvent : ChaosEvent
    {
        [SerializeField] private float abductionSeconds = 30f;

        public float AbductionSeconds => abductionSeconds;

        public static AlienAbductionEvent CreateDefault()
        {
            var e = CreateInstance<AlienAbductionEvent>();
            e.Configure("AlienAbduction", "Alien abduction", "Something is hovering over the dining room and it has picked you.", 1f, 1, 400f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<AlienAbductionRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="AlienAbductionEvent"/>: the saucer, the beam, the wait, the drop-off.</summary>
    public sealed class AlienAbductionRunner : ChaosEventRunner
    {
        private const float DescendSeconds = 2.5f;
        private const float HoverHeight = 11f;
        private static readonly Color SaucerColor = new Color(0.6f, 0.65f, 0.7f);
        private static readonly Color BeamColor = new Color(0.4f, 1f, 0.5f);

        private AlienAbductionEvent _event;
        private Transform _saucer;
        private Transform _beam;
        private float _timer;
        private bool _beamed;
        private float _leaveAt;

        protected override void OnBegin()
        {
            _event = Definition as AlienAbductionEvent;
            Vector3 over = Ctx.Player != null ? Ctx.Player.position : Ctx.FloorBounds.center;
            _saucer = new GameObject("Saucer").transform;
            _saucer.SetParent(transform, false);
            _saucer.position = new Vector3(over.x, HoverHeight + 8f, over.z);
            PrimitiveFactory.Visual("Hull", PrimitiveType.Sphere, _saucer, Vector3.zero, new Vector3(4f, 0.8f, 4f), MaterialLibrary.Get(SaucerColor));
            PrimitiveFactory.Visual("Dome", PrimitiveType.Sphere, _saucer, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 1f, 1.4f), MaterialLibrary.Get(new Color(0.5f, 0.9f, 1f)));
            for (int i = 0; i < 8; i++)
            {
                Vector3 at = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 1.7f;
                PrimitiveFactory.Visual($"Light {i + 1}", PrimitiveType.Sphere, _saucer, at - Vector3.up * 0.3f, Vector3.one * 0.25f, MaterialLibrary.Get(i % 2 == 0 ? Color.red : Color.green));
            }
            _beam = PrimitiveFactory.Visual("Beam", PrimitiveType.Cylinder, _saucer, new Vector3(0f, -HoverHeight * 0.5f, 0f), new Vector3(1.6f, HoverHeight * 0.5f, 1.6f), MaterialLibrary.Get(BeamColor)).transform;
            _beam.gameObject.SetActive(false);
            GameEvents.RaiseNotice("(audio cue: a theremin, then a hum you feel in your teeth) Something big is over the dining room.");
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;
            _timer += dt;
            _saucer.Rotate(0f, 90f * dt, 0f, Space.World);

            if (!_beamed)
            {
                Vector3 over = Ctx.Player != null ? Ctx.Player.position : _saucer.position;
                float t = Mathf.Clamp01(_timer / DescendSeconds);
                _saucer.position = new Vector3(over.x, Mathf.Lerp(HoverHeight + 8f, HoverHeight, Mathf.SmoothStep(0f, 1f, t)), over.z);
                if (t >= 1f) Beam();
                return;
            }

            if (Time.time >= _leaveAt)
            {
                _saucer.position += Vector3.up * (40f * dt);
                if (_saucer.position.y > HoverHeight + 40f) Finish(true, "You were returned to the front of the store");
            }
        }

        private void Beam()
        {
            _beamed = true;
            float seconds = _event != null ? _event.AbductionSeconds : 30f;
            _beam.gameObject.SetActive(true);
            GameEvents.RaiseLightingCueRequested(new Color(0.5f, 1f, 0.6f), 4f);
            GameEvents.RaisePlayerEffectRequested(new PlayerEffect { Kind = PlayerEffectKind.Abduct, Seconds = seconds, Position = Ctx.DoorInside, Source = "aliens" });
            GameEvents.RaiseNotice($"You are going up. The customers watch, chewing. Back in about {Mathf.RoundToInt(seconds)} seconds, at the front door, with nothing in your hands.");
            // The saucer hovers for as long as it has you (the effect lasts at least four seconds), then lifts away.
            _leaveAt = Time.time + Mathf.Max(4f, seconds);
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
