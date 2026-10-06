using BuffetSim.Bootstrap;
using BuffetSim.Food;
using BuffetSim.Models;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Customers
{
    /// <summary>
    /// Spawns placeholder customers at the door on a timer while the line has room. Each customer
    /// gets its order rolled immediately so its pathing is known the moment it steps in line.
    /// </summary>
    public sealed class CustomerSpawner : MonoBehaviour
    {
        private static readonly string[] Names =
        {
            "Dave", "Linda", "Marcus", "Priya", "Tommy", "Gloria", "Hank", "Mei", "Carl", "Rosa",
            "Big Steve", "Aunt Pat", "Kevin", "Yolanda", "Doug", "Fatima", "Rick", "Bev",
        };

        private CustomerContext _ctx;
        private FoodCatalog _catalog;
        private Vector3 _spawnPoint;
        private Font _labelFont;
        private float _timer;
        private int _spawnedCount;
        private bool _ready;

        public int SpawnedCount => _spawnedCount;

        public void Initialize(CustomerContext context, FoodCatalog catalog, Vector3 spawnPoint, Font labelFont, float firstSpawnDelay = 2f)
        {
            _ctx = context;
            _catalog = catalog;
            _spawnPoint = spawnPoint;
            _labelFont = labelFont;
            _timer = firstSpawnDelay;
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = _ctx.Config.CustomerSpawnInterval;
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
            Color shirt = Color.HSVToRGB((float)_ctx.Rng.NextDouble(), 0.55f, 0.9f);
            GameObject body = PrimitiveFactory.Solid("Body", PrimitiveType.Capsule, placeholder.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), MaterialLibrary.Get(shirt));
            body.layer = LayerMask.NameToLayer("Ignore Raycast");
            PrimitiveFactory.Visual("Head", PrimitiveType.Sphere, placeholder.transform, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.42f, MaterialLibrary.Get(new Color(0.95f, 0.8f, 0.65f)));
            PrimitiveFactory.Visual("Nose", PrimitiveType.Cube, placeholder.transform, new Vector3(0f, 1.95f, 0.22f), new Vector3(0.08f, 0.08f, 0.08f), MaterialLibrary.Get(new Color(0.9f, 0.6f, 0.5f)));

            TextMesh label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 2.45f, 0f), string.Empty, 0.22f, _labelFont, Color.white);
            label.gameObject.AddComponent<Billboard>();

            GlbModelLoader loader = root.AddComponent<GlbModelLoader>();
            loader.Configure("Models/customer.glb", placeholder);

            CustomerOrder order = CustomerOrder.Roll(_catalog.Unlocked, _ctx.Config, _ctx.Rng);
            CustomerAgent customer = root.AddComponent<CustomerAgent>();
            customer.Initialize(_ctx, order, customerName, label);
            return customer;
        }
    }
}
