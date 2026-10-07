using System.Collections.Generic;
using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Food;
using BuffetSim.Models;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Customers
{
    /// <summary>
    /// Spawns placeholder customers at the door on a timer while the line has room and the doors
    /// are open. Each customer gets its order rolled immediately so its pathing is known the moment
    /// it steps in line. Keeps the live list of customers in the building and publishes the count;
    /// satisfaction scales how quickly the next customer shows up.
    /// </summary>
    public sealed class CustomerSpawner : MonoBehaviour
    {
        private static readonly string[] Names =
        {
            "Dave", "Linda", "Marcus", "Priya", "Tommy", "Gloria", "Hank", "Mei", "Carl", "Rosa",
            "Big Steve", "Aunt Pat", "Kevin", "Yolanda", "Doug", "Fatima", "Rick", "Bev",
        };

        private static readonly System.Predicate<CustomerAgent> IsGone = customer => customer == null;

        // Shirt hues come from a small palette so MaterialLibrary reuses the same few materials instead of one per customer.
        private const int ShirtHues = 8;

        private readonly List<CustomerAgent> _customers = new List<CustomerAgent>();

        private CustomerContext _ctx;
        private FoodCatalog _catalog;
        private Vector3 _spawnPoint;
        private Font _labelFont;
        private float _timer;
        private float _firstSpawnDelay;
        private int _spawnedCount;
        private bool _ready;
        // Defaults let the spawner behave as before when no DayClock or StoreReputation exists.
        private DayPhase _phase = DayPhase.Open;
        private float _reputation = 50f;
        private int _maxedOrders;

        public int SpawnedCount => _spawnedCount;

        /// <summary>Everyone this spawner put in the building who has not left yet (destroyed entries are pruned each frame).</summary>
        public IReadOnlyList<CustomerAgent> Customers => _customers;

        /// <summary>How many customers are currently in the building.</summary>
        public int InStore
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _customers.Count; i++)
                {
                    if (_customers[i] != null) count++;
                }
                return count;
            }
        }

        public void Initialize(CustomerContext context, FoodCatalog catalog, Vector3 spawnPoint, Font labelFont, float firstSpawnDelay = 2f)
        {
            _ctx = context;
            _catalog = catalog;
            _spawnPoint = spawnPoint;
            _labelFont = labelFont;
            _firstSpawnDelay = firstSpawnDelay;
            _timer = firstSpawnDelay;
            // StoreReputation publishes its opening value before this spawner subscribes, so seed from the config.
            if (context.Config != null) _reputation = Mathf.Clamp(context.Config.StartingReputation, 0f, 100f);
            _ready = true;
        }

        private void OnEnable()
        {
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
            GameEvents.ReputationChanged += OnReputationChanged;
            GameEvents.CustomerSpawnRequested += OnSpawnRequested;
            GameEvents.OrderBoostRequested += OnOrderBoostRequested;
            GameEvents.DashersTripRequested += OnDashersTripRequested;
            GameEvents.CustomerFollowRequested += OnFollowRequested;
        }

        private void OnDisable()
        {
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
            GameEvents.ReputationChanged -= OnReputationChanged;
            GameEvents.CustomerSpawnRequested -= OnSpawnRequested;
            GameEvents.OrderBoostRequested -= OnOrderBoostRequested;
            GameEvents.DashersTripRequested -= OnDashersTripRequested;
            GameEvents.CustomerFollowRequested -= OnFollowRequested;
        }

        private void Update()
        {
            if (!_ready) return;

            // Customers destroyed by anything other than their own Finished (knock-outs, events) drop out here.
            int before = _customers.Count;
            _customers.RemoveAll(IsGone);
            if (_customers.Count != before) PublishCount();

            if (_phase != DayPhase.Open) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = _ctx.Config.CustomerSpawnInterval * SpawnIntervalScale();
            if (_ctx.Queue.Count >= _ctx.Config.MaxLineLength) return;
            Spawn();
        }

        public CustomerAgent Spawn()
        {
            _spawnedCount++;
            string customerName = Names[_ctx.Rng.Next(Names.Length)];

            var root = new GameObject($"Customer {_spawnedCount} ({customerName})");
            root.transform.SetParent(transform, false);

            Vector3 position = _spawnPoint;
            if (NavMesh.SamplePosition(_spawnPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas)) position = hit.position;
            root.transform.position = position;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 2.4f;
            agent.acceleration = 14f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.25f;
            agent.avoidancePriority = _ctx.Rng.Next(30, 70);

            // Placeholder body: capsule + head. A GLB at StreamingAssets/Models/customer.glb replaces it.
            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(root.transform, false);
            Color shirt = Color.HSVToRGB(_ctx.Rng.Next(ShirtHues) / (float)ShirtHues, 0.55f, 0.9f);
            GameObject body = PrimitiveFactory.Solid("Body", PrimitiveType.Capsule, placeholder.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), MaterialLibrary.Get(shirt));
            body.layer = LayerMask.NameToLayer("Ignore Raycast");
            PrimitiveFactory.Visual("Head", PrimitiveType.Sphere, placeholder.transform, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.42f, MaterialLibrary.Get(new Color(0.95f, 0.8f, 0.65f)));
            PrimitiveFactory.Visual("Nose", PrimitiveType.Cube, placeholder.transform, new Vector3(0f, 1.95f, 0.22f), new Vector3(0.08f, 0.08f, 0.08f), MaterialLibrary.Get(new Color(0.9f, 0.6f, 0.5f)));

            TextMesh label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 2.45f, 0f), string.Empty, 0.22f, _labelFont, Color.white);
            label.gameObject.AddComponent<Billboard>();

            GlbModelLoader loader = root.AddComponent<GlbModelLoader>();
            loader.Configure("Models/customer.glb", placeholder);

            bool maxed = _maxedOrders > 0;
            if (maxed) _maxedOrders--;
            CustomerOrder order = CustomerOrder.Roll(_catalog.Unlocked, _ctx.Config, _ctx.Rng, maxed);
            CustomerAgent customer = root.AddComponent<CustomerAgent>();
            customer.Initialize(_ctx, order, customerName, label);

            _customers.Add(customer);
            customer.Finished += OnCustomerFinished;
            PublishCount();
            return customer;
        }

        private void OnCustomerFinished(CustomerAgent customer)
        {
            if (customer != null) customer.Finished -= OnCustomerFinished;
            _customers.Remove(customer);
            PublishCount();
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            DayPhase previous = _phase;
            _phase = snapshot.Phase;

            if (_phase == DayPhase.Closed)
            {
                ClearCustomers();
            }
            else if (_phase == DayPhase.Open && previous != DayPhase.Open)
            {
                // New day: don't make the first customer wait out a whole interval.
                _timer = Mathf.Min(_timer, _firstSpawnDelay);
            }
        }

        private void OnReputationChanged(float value, float delta, string reason)
        {
            _reputation = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>Let people in right now, line length be damned (a tour bus, the noise of 8-8-8). Not after the doors close.</summary>
        private void OnSpawnRequested(int count)
        {
            if (!_ready || _phase == DayPhase.Closing || _phase == DayPhase.Closed) return;
            for (int i = 0; i < count; i++) Spawn();
        }

        private void OnOrderBoostRequested(int customers)
        {
            if (customers > 0) _maxedOrders += customers;
        }

        private void OnDashersTripRequested(float seconds)
        {
            if (!_ready || seconds <= 0f) return;
            _ctx.DashersTripUntil = Mathf.Max(_ctx.DashersTripUntil, Time.time) + seconds;
        }

        /// <summary>The seated customer nearest the target gets up and trails it.</summary>
        private void OnFollowRequested(Transform target, float seconds)
        {
            if (!_ready || target == null) return;
            CustomerAgent nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < _customers.Count; i++)
            {
                CustomerAgent customer = _customers[i];
                if (customer == null || !customer.IsSeated || customer.IsHeld) continue;
                float distance = (customer.transform.position - target.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = customer;
                }
            }

            if (nearest != null && nearest.Follow(target, seconds))
                GameEvents.RaiseNotice($"{nearest.CustomerName} put down their fork and started following you. They don't say why.");
            else
                GameEvents.RaiseNotice("Nobody is sitting down, so nobody is thinking of you.");
        }

        /// <summary>Everyone still inside vanishes with the lights.</summary>
        private void ClearCustomers()
        {
            // Copy first: destroying an agent runs its OnDestroy (queue/table release) and must not touch the live list mid-loop.
            CustomerAgent[] remaining = _customers.ToArray();
            _customers.Clear();
            for (int i = 0; i < remaining.Length; i++)
            {
                CustomerAgent customer = remaining[i];
                if (customer == null) continue;
                customer.Finished -= OnCustomerFinished;
                Destroy(customer.gameObject);
            }
            GameEvents.RaiseCustomerCountChanged(0);
        }

        private float SpawnIntervalScale()
        {
            return Mathf.Lerp(_ctx.Config.SpawnScaleAtZeroReputation, _ctx.Config.SpawnScaleAtFullReputation, Mathf.Clamp01(_reputation / 100f));
        }

        private void PublishCount()
        {
            GameEvents.RaiseCustomerCountChanged(InStore);
        }
    }
}
