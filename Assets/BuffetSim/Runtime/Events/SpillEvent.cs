using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A kid knocked over the sweet and sour: puddles appear on the dining floor and stay until the
    /// player mops them (three presses each). Customers and the player slip on them. No timer; the
    /// event resolves when the floor is clean, and day end clears what is left.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Spill", fileName = "Spill")]
    public sealed class SpillEvent : ChaosEvent
    {
        [SerializeField] private int puddleCount = 3;
        [Tooltip("Minimum distance between puddles.")]
        [SerializeField] private float minSpacing = 1.5f;
        [Tooltip("No puddle closer than this to the register point.")]
        [SerializeField] private float registerClearance = 3f;

        public int PuddleCount => puddleCount;
        public float MinSpacing => minSpacing;
        public float RegisterClearance => registerClearance;

        public static SpillEvent CreateDefault()
        {
            var e = CreateInstance<SpillEvent>();
            e.Configure("Spill", "Spill", "A kid knocked over the sweet and sour.", 1f, 1, 150f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<SpillRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="SpillEvent"/>: places the puddles and waits for the last one to be mopped.</summary>
    public sealed class SpillRunner : ChaosEventRunner
    {
        private const int PlacementAttempts = 40;
        private static readonly Color SauceColor = new Color(0.55f, 0.05f, 0.08f);

        private readonly List<SpillPuddle> _puddles = new List<SpillPuddle>();
        private readonly List<Vector3> _placed = new List<Vector3>();
        private bool _placedAll;

        public int PuddlesLeft => _puddles.Count;

        protected override void OnBegin()
        {
            var e = Definition as SpillEvent;
            int count = e != null ? Mathf.Max(1, e.PuddleCount) : 3;
            float spacing = e != null ? e.MinSpacing : 1.5f;
            float clearance = e != null ? e.RegisterClearance : 3f;

            for (int i = 0; i < count; i++)
            {
                Vector3 point = PickPoint(spacing, clearance);
                _placed.Add(point);
                _puddles.Add(SpawnPuddle(point, i));
            }
            _placedAll = true;
            GameEvents.RaiseNotice("Sweet and sour everywhere. Mop it up before someone goes down.");
            if (_puddles.Count == 0) Finish(true, "Floor mopped");
        }

        /// <summary>A walkable point in the dining area, spaced from the other puddles and clear of the register; falls back to any walkable point.</summary>
        private Vector3 PickPoint(float spacing, float clearance)
        {
            Vector3 fallback = Vector3.zero;
            bool haveFallback = false;
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                Vector3 candidate = ChaosActors.RandomPointInBounds(Ctx.FloorBounds, Ctx.Rng);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                Vector3 point = hit.position;
                if (!haveFallback)
                {
                    fallback = point;
                    haveFallback = true;
                }
                if (ChaosActors.HorizontalDistance(point, Ctx.RegisterPoint) < clearance) continue;

                bool tooClose = false;
                for (int i = 0; i < _placed.Count; i++)
                {
                    if (ChaosActors.HorizontalDistance(point, _placed[i]) < spacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;
                return point;
            }
            return haveFallback ? fallback : ChaosActors.SampleNavMesh(Ctx.FloorBounds.center, 5f);
        }

        private SpillPuddle SpawnPuddle(Vector3 point, int index)
        {
            var root = new GameObject($"Spill Puddle {index + 1}");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(point.x, ChaosActors.FloorHeightAt(point, point.y), point.z);
            root.layer = 0;

            // A flat disc, slightly squashed and turned so three of them don't look identical. No collider of its own.
            float stretch = 0.85f + (float)Ctx.Rng.NextDouble() * 0.3f;
            GameObject disc = PrimitiveFactory.Visual("Sauce", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(1.1f * stretch, 0.01f, 1.1f / stretch), MaterialLibrary.Get(SauceColor));
            disc.transform.localRotation = Quaternion.Euler(0f, (float)Ctx.Rng.NextDouble() * 360f, 0f);

            // Trigger box on the root so the player's interaction ray can find the puddle without blocking anyone.
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.15f, 0f);
            trigger.size = new Vector3(1.1f, 0.3f, 1.1f);

            SpillPuddle puddle = root.AddComponent<SpillPuddle>();
            puddle.Initialize(this, Ctx, disc.transform);
            return puddle;
        }

        /// <summary>Called by a puddle when it is destroyed (mopped or cleaned up).</summary>
        public void OnPuddleGone(SpillPuddle puddle)
        {
            _puddles.Remove(puddle);
            if (IsFinished || !_placedAll) return;
            if (_puddles.Count == 0) Finish(true, "Floor mopped");
        }

        public override void Abort()
        {
            // The puddles are children of this runner and go with it.
            _puddles.Clear();
            EndSilently();
        }
    }
}
