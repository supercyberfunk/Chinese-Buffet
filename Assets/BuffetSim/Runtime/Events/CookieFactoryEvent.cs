using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Player;
using BuffetSim.UI;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// The fortune cookie machine jams open and fires two hundred blank cookies at the front door
    /// over half a minute. The pile blocks the line: nobody in it can get in, and they say so.
    /// Scoop the pile onto a tray twenty at a time (E) and bin it. Leave it long enough and the
    /// line kicks the rest into the parking lot, which is not the same thing as cleaning it up.
    /// Weight 0: only a fortune starts this one.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Cookie Factory", fileName = "CookieFactory")]
    public sealed class CookieFactoryEvent : ChaosEvent
    {
        [SerializeField] private int totalCookies = 200;
        [SerializeField] private float fireSeconds = 30f;
        [SerializeField] private int scoopSize = 20;
        [SerializeField] private float blockInterval = 10f;
        [Range(0f, 1f)] [SerializeField] private float patienceLoss = 0.34f;
        [SerializeField] private float giveUpSeconds = 240f;

        public int TotalCookies => totalCookies;
        public float FireSeconds => fireSeconds;
        public int ScoopSize => scoopSize;
        public float BlockInterval => blockInterval;
        public float PatienceLoss => patienceLoss;
        public float GiveUpSeconds => giveUpSeconds;

        public static CookieFactoryEvent CreateDefault()
        {
            var e = CreateInstance<CookieFactoryEvent>();
            e.Configure("CookieFactory", "Cookie factory", "The slot machine jammed open. It is firing blank cookies at the front door.", 0f, 1, 60f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<CookieFactoryRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="CookieFactoryEvent"/>: the barrage, the mound at the door, the scooping, or the line's boot.</summary>
    public sealed class CookieFactoryRunner : ChaosEventRunner
    {
        private const string ItemId = "blank-cookies";
        private const int MoundSpheres = 10;
        private const float ShotInterval = 0.5f;
        private const float ShotFlightSeconds = 0.6f;
        private const float ShotArcHeight = 1.1f;
        private const float ShotSize = 0.14f;
        private static readonly Color CookieColor = new Color(0.85f, 0.7f, 0.45f);
        private static readonly Color LabelColor = new Color(1f, 0.9f, 0.7f);
        private static readonly Vector3 PileOffset = new Vector3(-1.2f, 0f, 0.6f);
        private static readonly Vector3 MachineOffset = new Vector3(-5f, 1f, 0f);

        private CookieFactoryEvent _event;
        private GameObject _pile;
        private Transform[] _mound;
        private Vector3[] _moundRest;
        private float[] _moundSize;
        private TextMesh _label;
        private EventActor _actor;
        private Vector3 _machinePoint;
        private int _totalCookies;
        private int _scoopSize;
        private int _cookies;
        private int _firedSoFar;
        private int _scooped;
        private float _fireElapsed;
        private float _shotTimer;
        private float _blockTimer;
        private float _giveUpTimer;
        private bool _firing;

        protected override void OnBegin()
        {
            _event = Definition as CookieFactoryEvent;
            _totalCookies = _event != null ? Mathf.Max(1, _event.TotalCookies) : 200;
            _scoopSize = _event != null ? Mathf.Max(1, _event.ScoopSize) : 20;
            _blockTimer = _event != null ? Mathf.Max(1f, _event.BlockInterval) : 10f;
            _giveUpTimer = _event != null ? Mathf.Max(1f, _event.GiveUpSeconds) : 240f;
            _machinePoint = Ctx.DoorInside + MachineOffset;

            Vector3 pilePoint = Ctx.DoorInside + PileOffset;
            pilePoint.y = ChaosActors.FloorHeightAt(pilePoint, pilePoint.y);
            BuildPile(pilePoint);

            _actor = EventActor.Attach(_pile, 1.0f, 1.0f);
            _actor.AimHeight = 0.5f;
            _actor.Accepts = kind => false;
            _actor.Prompt = inv => inv != null && inv.HandsFree
                ? $"[E] Scoop {Mathf.Min(_scoopSize, _cookies)} blank cookies onto a tray ({_cookies} left)"
                : $"Blank cookies ({_cookies} left) - free your hands to scoop";
            _actor.OnInteract = Scoop;

            _firing = true;
            _shotTimer = 0f;
            RefreshPile();
            GameEvents.RaiseNotice("The fortune cookie machine has jammed open and is firing blank cookies at the front door. Nobody asked it to. (audio cue: a slot machine coughing, two hundred times)");
        }

        private void BuildPile(Vector3 at)
        {
            _pile = new GameObject("Cookie Pile");
            _pile.transform.SetParent(transform, false);
            _pile.transform.position = at;
            _pile.layer = 0; // Default: the player's interaction ray must be able to hit it.

            Material tan = MaterialLibrary.Get(CookieColor);
            _mound = new Transform[MoundSpheres];
            _moundRest = new Vector3[MoundSpheres];
            _moundSize = new float[MoundSpheres];
            for (int i = 0; i < MoundSpheres; i++)
            {
                bool core = i < 3;
                float jitter = (float)Ctx.Rng.NextDouble();
                float angle = core
                    ? (i * 120f + jitter * 40f) * Mathf.Deg2Rad
                    : ((i - 3) * (360f / (MoundSpheres - 3)) + jitter * 25f) * Mathf.Deg2Rad;
                float radius = core ? 0.18f : 0.48f + jitter * 0.12f;
                _moundRest[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _moundSize[i] = core ? 0.5f + jitter * 0.1f : 0.26f + jitter * 0.16f;
                GameObject sphere = PrimitiveFactory.Visual($"Cookies {i}", PrimitiveType.Sphere, _pile.transform, _moundRest[i], Vector3.one * _moundSize[i], tan);
                _mound[i] = sphere.transform;
            }

            _label = PrimitiveFactory.Label("Label", _pile.transform, new Vector3(0f, 1.1f, 0f), "blank cookies: 0", 0.18f, Ctx.Font, LabelColor);
            _label.gameObject.AddComponent<Billboard>();
        }

        private void Update()
        {
            if (IsFinished) return;
            float dt = Time.deltaTime;

            if (_firing) TickFiring(dt);

            _blockTimer -= dt;
            if (_blockTimer <= 0f)
            {
                _blockTimer = _event != null ? Mathf.Max(1f, _event.BlockInterval) : 10f;
                if (_cookies > 0) BlockSomeone();
            }

            _giveUpTimer -= dt;
            if (_giveUpTimer <= 0f) GiveUp();
        }

        private void TickFiring(float dt)
        {
            float fireSeconds = _event != null ? Mathf.Max(0.1f, _event.FireSeconds) : 30f;
            _fireElapsed += dt;
            int target = Mathf.RoundToInt(_totalCookies * Mathf.Clamp01(_fireElapsed / fireSeconds));
            if (target > _firedSoFar)
            {
                _cookies += target - _firedSoFar;
                _firedSoFar = target;
                RefreshPile();
            }

            _shotTimer -= dt;
            if (_shotTimer <= 0f)
            {
                _shotTimer = ShotInterval;
                FireShot();
                FireShot();
            }

            if (_fireElapsed >= fireSeconds)
            {
                _firing = false;
                GameEvents.RaiseNotice($"The machine ran dry. {_cookies} blank cookies are blocking the front door and none of them have anything to say. (audio cue: one last cough, then a fan)");
                if (_cookies <= 0) Resolve();
            }
        }

        /// <summary>One tan sphere from the machine to the top of the mound, destroyed when it lands.</summary>
        private void FireShot()
        {
            float sx = ((float)Ctx.Rng.NextDouble() - 0.5f) * 0.8f;
            float sz = ((float)Ctx.Rng.NextDouble() - 0.5f) * 0.8f;
            Vector3 from = _machinePoint + new Vector3(0f, sx * 0.25f, sz * 0.5f);
            Vector3 to = _pile.transform.position + new Vector3(sx, 0.3f, sz);
            GameObject shot = PrimitiveFactory.Visual("Blank Cookie", PrimitiveType.Sphere, transform, from, Vector3.one * ShotSize, MaterialLibrary.Get(CookieColor));
            StartCoroutine(Fly(shot.transform, from, to));
        }

        private System.Collections.IEnumerator Fly(Transform shot, Vector3 from, Vector3 to)
        {
            float t = 0f;
            while (t < 1f)
            {
                if (shot == null) yield break;
                t += Time.deltaTime / ShotFlightSeconds;
                float k = Mathf.Clamp01(t);
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * ShotArcHeight;
                shot.position = p;
                yield return null;
            }
            if (shot != null) Destroy(shot.gameObject);
        }

        /// <summary>Sizes the mound to the count and keeps the label honest.</summary>
        private void RefreshPile()
        {
            float growth = Mathf.Clamp01((float)_cookies / _totalCookies);
            float f = Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(growth));
            bool visible = _cookies > 0;
            for (int i = 0; i < _mound.Length; i++)
            {
                Transform sphere = _mound[i];
                if (sphere == null) continue;
                sphere.gameObject.SetActive(visible);
                float s = _moundSize[i] * f;
                sphere.localScale = Vector3.one * s;
                sphere.localPosition = new Vector3(_moundRest[i].x * f, s * 0.42f, _moundRest[i].z * f);
            }
            if (_label != null) _label.text = $"blank cookies: {_cookies}";
        }

        private void BlockSomeone()
        {
            CustomerAgent customer = ChaosActors.PickCustomer(Ctx, c =>
                !c.IsSeated && !c.IsPaid && !c.IsKnockedOut &&
                (c.CurrentState == CustomerAgent.State.WalkingToLine || c.CurrentState == CustomerAgent.State.WaitingInLine));
            if (customer == null) return;
            customer.LosePatience(_event != null ? _event.PatienceLoss : 0.34f);
            string name = customer.CustomerName;
            GameEvents.RaiseNotice(ChaosActors.Pick(Ctx.Rng,
                $"{name} can't get through the pile of blank cookies at the door. They have read the one on top. It is also blank.",
                $"{name} is standing outside, looking at {_cookies} blank cookies and then at you. (audio cue: a shoe crunching through a cookie)",
                $"{name} tried to wade through the cookies at the door and gave up halfway. The crunching is getting to everyone.",
                $"{name} asked whether the cookies at the door are a promotion. Nobody answered. Patience is going."));
        }

        private void Scoop(PlayerInventory inventory)
        {
            if (IsFinished) return;
            if (_cookies <= 0)
            {
                GameEvents.RaiseNotice("Nothing on the floor yet. The machine is winding up. (audio cue: a hopper rattling)");
                return;
            }
            if (inventory == null || !inventory.HandsFree)
            {
                GameEvents.RaiseNotice("Your hands are full. The cookies will wait; they have nowhere to be.");
                return;
            }
            int take = Mathf.Min(_scoopSize, _cookies);
            if (!inventory.TryTakeItem(ItemId, "a tray of blank cookies", 1, false, false))
            {
                GameEvents.RaiseNotice("The tray didn't take. Try again with empty hands.");
                return;
            }
            _cookies -= take;
            _scooped += take;
            RefreshPile();
            GameEvents.RaiseNotice($"Scooped {take} blank cookies onto a tray ({_cookies} left). They are not food. The trash can is where they are going. (audio cue: a dustpan full of gravel)");
            if (!_firing && _cookies <= 0) Resolve();
        }

        private void Resolve()
        {
            if (IsFinished) return;
            _actor.Active = false;
            GameEvents.RaiseNotice("The last of the blank cookies are off the floor. The machine has gone quiet, which is somehow worse. The line is moving again.");
            Finish(true, $"{_scooped} blank cookies scooped");
        }

        private void GiveUp()
        {
            if (IsFinished) return;
            _firing = false;
            _actor.Active = false;
            int kicked = _cookies;
            _cookies = 0;
            RefreshPile();
            GameEvents.RaiseNotice($"The line has had enough. They kicked the last {kicked} blank cookies out into the parking lot, where a seagull is already investigating. (audio cue: {kicked} small crunches, then a car alarm)");
            Finish(false, $"{kicked} cookies kicked into the parking lot by the line");
        }

        public override void Abort()
        {
            EndSilently();
        }
    }
}
