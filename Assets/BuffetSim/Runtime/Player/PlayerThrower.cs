using System.Collections.Generic;
using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Items;
using UnityEngine;

namespace BuffetSim.Player
{
    /// <summary>
    /// The apron pocket's keys: Tab picks an item, left click uses it, R cracks a fortune cookie.
    /// A rock homes on the most deserving target in range (a runner before a diner), a dodgeball
    /// flies straight, a thrown cookie cracks on whatever it hits and the fortune comes back to you
    /// anyway, and a cigarette needs five seconds of standing still. Hits go through
    /// <see cref="IThrowTarget"/>; this script never touches customers or money itself.
    /// </summary>
    public sealed class PlayerThrower : MonoBehaviour
    {
        public const int RockMax = 3;
        public const int DodgeballMax = 6;
        public const int CigarettesMax = 20;
        public const int CookieMax = 5;

        private const float RockRange = 10f;
        private const float StraightRange = 9f;
        private const float ThrowSpeed = 16f;
        private const float SmokeLightSeconds = 5f;
        private const float SmokeBuffSeconds = 60f;
        private const float SmokeSpeed = 1.2f;

        private static readonly Color RockColor = new Color(0.45f, 0.43f, 0.4f);
        private static readonly Color DodgeballColor = new Color(0.85f, 0.15f, 0.15f);
        private static readonly Color CookieColor = new Color(0.9f, 0.7f, 0.35f);

        [SerializeField] private Camera viewCamera;

        private readonly HashSet<IThrowTarget> _seen = new HashSet<IThrowTarget>();
        private readonly Collider[] _overlap = new Collider[64];
        private PlayerPocket _pocket;
        private PlayerEffects _effects;
        private PlayerController _controller;
        private PlayerInteractor _interactor;
        private bool _wasLocked;
        private bool _lighting;
        private float _lightTimer;

        public Camera ViewCamera
        {
            get => viewCamera;
            set => viewCamera = value;
        }

        private void Start()
        {
            _pocket = GetComponent<PlayerPocket>();
            _effects = GetComponent<PlayerEffects>();
            _controller = GetComponent<PlayerController>();
            _interactor = GetComponent<PlayerInteractor>();
        }

        private void Update()
        {
            if (_pocket == null || viewCamera == null) return;

            // The click that re-locks the cursor must not also throw: act only if it was locked last frame too.
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool canAct = locked && _wasLocked && (_effects == null || !_effects.ControlsLocked) && (_interactor == null || !_interactor.IsChannelling);
            _wasLocked = locked;
            if (!canAct)
            {
                CancelLighting(null);
                return;
            }

            if (InputReader.CyclePocketPressed()) _pocket.CycleSelection();
            if (InputReader.CrackPressed()) CrackCookie();
            if (InputReader.ThrowPressed()) UseSelected();
            TickLighting();
        }

        private void CrackCookie()
        {
            if (!_pocket.TryRemove(PocketItems.Cookie))
            {
                GameEvents.RaiseNotice("No fortune cookies. The slot machine by the door has them, allegedly.");
                return;
            }
            GameEvents.RaiseFortuneCookieCracked(transform.position);
        }

        private void UseSelected()
        {
            PlayerPocket.Entry selected = _pocket.Selected;
            if (selected == null) return;

            switch (selected.Id)
            {
                case PocketItems.Rock:
                    ThrowRock();
                    break;
                case PocketItems.Dodgeball:
                    ThrowStraight(ThrowableKind.Dodgeball, PocketItems.Dodgeball, "dodgeball", DodgeballColor, 0.3f);
                    break;
                case PocketItems.Cookie:
                    ThrowStraight(ThrowableKind.Cookie, PocketItems.Cookie, "fortune cookie", CookieColor, 0.16f);
                    break;
                case PocketItems.Cigarettes:
                    StartLighting();
                    break;
            }
        }

        // ----- Rock: homes on the best target in range -----

        private void ThrowRock()
        {
            if (!_pocket.TryRemove(PocketItems.Rock)) return;
            Vector3 hand = HandPosition();
            IThrowTarget target = FindHomingTarget(ThrowableKind.Rock, RockRange);
            // With nothing to home on, the rock goes where you are looking and stops at the first wall, not through it.
            FindStraightTarget(ThrowableKind.Rock, StraightRange, out Vector3 miss);

            ThrownObject.Launch(PrimitiveType.Sphere, 0.18f, RockColor, hand, miss, target, ThrowSpeed, target != null ? 1.2f : 0.5f,
                (thrown, hit, at) =>
                {
                    if (hit != null && hit.AcceptsThrow(ThrowableKind.Rock))
                    {
                        hit.OnThrowHit(ThrowableKind.Rock);
                        return;
                    }
                    // Nothing to home on (or it got away): it's a rock on the floor again.
                    PocketPickup.Spawn(PocketItems.Rock, "rock", 1, RockMax, at, RockColor, PrimitiveType.Sphere, 0.2f);
                });
        }

        private IThrowTarget FindHomingTarget(ThrowableKind kind, float range)
        {
            Transform cam = viewCamera.transform;
            // Default layers only: every throw target's trigger lives there, and it keeps the Ignore Raycast bodies out of the buffer.
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            _seen.Clear();
            IThrowTarget best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlap[i];
                if (c == null) continue;
                IThrowTarget target = c.GetComponentInParent<IThrowTarget>();
                if (target == null || !_seen.Add(target)) continue;
                if (target.HomingPriority <= 0 || !target.AcceptsThrow(kind)) continue;

                Vector3 to = target.AimPoint - cam.position;
                float distance = to.magnitude;
                if (distance > range) continue;
                float facing = distance > 0.01f ? Vector3.Dot(cam.forward, to / distance) : 1f;
                // Priority first (a runner beats a diner), then whatever you're looking at, then the closest.
                float score = target.HomingPriority * 100f + facing * 10f - distance * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }
            return best;
        }

        // ----- Dodgeball and cookie: straight throws -----

        private void ThrowStraight(ThrowableKind kind, string pocketId, string displayName, Color color, float size)
        {
            if (!_pocket.TryRemove(pocketId)) return;
            Vector3 hand = HandPosition();
            IThrowTarget target = FindStraightTarget(kind, StraightRange, out Vector3 landing);

            ThrownObject.Launch(PrimitiveType.Sphere, size, color, hand, landing, target, ThrowSpeed, 0.25f,
                (thrown, hit, at) => OnStraightLanded(kind, hit, at, displayName, color, size));
        }

        private void OnStraightLanded(ThrowableKind kind, IThrowTarget hit, Vector3 at, string displayName, Color color, float size)
        {
            bool accepted = hit != null && hit.AcceptsThrow(kind);
            if (accepted) hit.OnThrowHit(kind);

            switch (kind)
            {
                case ThrowableKind.Dodgeball:
                    // It bounces off and can be picked up again; a bounce into a wall or a table stays where it landed instead.
                    Vector3 bounce = at + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                    if (Physics.CheckSphere(bounce + Vector3.up * 0.15f, 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) bounce = at;
                    PocketPickup.Spawn(PocketItems.Dodgeball, displayName, 1, DodgeballMax, bounce, color, PrimitiveType.Sphere, size);
                    break;
                case ThrowableKind.Cookie:
                    GameEvents.RaiseNotice(accepted
                        ? "The cookie cracked on impact and the fortune drifted straight back to you. You cannot dodge your own fortune."
                        : "The cookie shattered on the floor. The slip blew back into your hand. You cannot dodge your own fortune.");
                    GameEvents.RaiseFortuneCookieCracked(transform.position);
                    break;
            }
        }

        /// <summary>The first thing along your view that takes this throw; walls and counters stop it. Null if nothing does.</summary>
        private IThrowTarget FindStraightTarget(ThrowableKind kind, float range, out Vector3 landing)
        {
            Transform cam = viewCamera.transform;
            landing = StraightLanding(range);
            RaycastHit[] hits = Physics.SphereCastAll(cam.position, 0.3f, cam.forward, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                IThrowTarget target = c.GetComponentInParent<IThrowTarget>();
                if (target != null && target.AcceptsThrow(kind))
                {
                    landing = target.AimPoint;
                    return target;
                }
                if (!c.isTrigger)
                {
                    // A wall, a table, the counter: it stops there and drops.
                    Vector3 point = hits[i].distance > 0f ? hits[i].point : cam.position + cam.forward * 0.5f;
                    landing = new Vector3(point.x, 0f, point.z) - new Vector3(cam.forward.x, 0f, cam.forward.z).normalized * 0.3f;
                    return null;
                }
            }
            return null;
        }

        private Vector3 StraightLanding(float range)
        {
            Transform cam = viewCamera.transform;
            Vector3 flat = new Vector3(cam.forward.x, 0f, cam.forward.z);
            if (flat.sqrMagnitude < 0.001f) flat = transform.forward;
            Vector3 end = transform.position + flat.normalized * (range * 0.8f);
            return new Vector3(end.x, 0f, end.z);
        }

        private Vector3 HandPosition()
        {
            Transform cam = viewCamera.transform;
            return cam.position + cam.forward * 0.5f + cam.right * 0.2f - cam.up * 0.2f;
        }

        // ----- Cigarettes -----

        private void StartLighting()
        {
            if (_lighting) return;
            if (_pocket.Count(PocketItems.Cigarettes) <= 0) return;
            _lighting = true;
            _lightTimer = SmokeLightSeconds;
            GameEvents.RaiseNotice("Lighting up. Stand still for five seconds.");
        }

        private void TickLighting()
        {
            if (!_lighting) return;
            if (_controller != null && _controller.IsMoving)
            {
                CancelLighting("You moved. The lighter went out.");
                return;
            }

            _lightTimer -= Time.deltaTime;
            if (_lightTimer > 0f) return;

            _lighting = false;
            if (!_pocket.TryRemove(PocketItems.Cigarettes)) return;
            if (_effects != null)
                _effects.Apply(new PlayerEffect { Kind = PlayerEffectKind.SpeedMultiplier, Value = SmokeSpeed, Seconds = SmokeBuffSeconds, Source = "a cigarette" });
            GameEvents.RaiseNotice("You light up. Nobody in the building says a word. (+20% speed for a minute)");
        }

        private void CancelLighting(string notice)
        {
            if (!_lighting) return;
            _lighting = false;
            if (!string.IsNullOrEmpty(notice)) GameEvents.RaiseNotice(notice);
        }
    }
}
