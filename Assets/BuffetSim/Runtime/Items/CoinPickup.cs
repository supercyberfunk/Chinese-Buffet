using BuffetSim.Bootstrap;
using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Items
{
    /// <summary>
    /// A coin that sprays out of a knocked-out dasher or thief (the "Sonic rings" moment from the
    /// notes), lands on the floor and gets sucked into the player when they walk near it. Picking one
    /// up publishes <see cref="GameEvents.MoneyRecovered"/>; the ledger decides what that is worth.
    /// Coins left on the floor disappear when the day ends.
    /// </summary>
    public sealed class CoinPickup : MonoBehaviour
    {
        private const float FlightSeconds = 0.7f;
        private const float MagnetRadius = 1.7f;
        private const float CollectRadius = 0.55f;
        private const float MagnetSpeed = 7f;
        private const float RestHeight = 0.12f;

        private static readonly Color CoinColor = new Color(1f, 0.84f, 0.2f);

        private float _amount;
        private string _reason;
        private Transform _collector;
        private Vector3 _start;
        private Vector3 _landing;
        private float _arcHeight;
        private float _age;
        private bool _landed;
        private bool _collected;

        /// <summary>
        /// Sprays <paramref name="totalAmount"/> as <paramref name="coinCount"/> coins around
        /// <paramref name="origin"/>. The collector defaults to the main camera's rig (the player).
        /// </summary>
        public static void Burst(Vector3 origin, float totalAmount, int coinCount, string reason, Transform collector = null)
        {
            if (totalAmount <= 0f) return;
            coinCount = Mathf.Max(1, coinCount);
            if (collector == null && Camera.main != null) collector = Camera.main.transform;

            float perCoin = totalAmount / coinCount;
            for (int i = 0; i < coinCount; i++)
            {
                // Spread the burst evenly around the body so the pile reads as "it came out of them".
                float angle = (360f / coinCount) * i + Random.Range(-18f, 18f);
                float distance = Random.Range(0.8f, 2.4f);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                Spawn(origin + Vector3.up * 1.0f, origin + offset, perCoin, reason, collector);
            }
        }

        public static CoinPickup Spawn(Vector3 from, Vector3 landing, float amount, string reason, Transform collector)
        {
            GameObject go = PrimitiveFactory.Visual("Coin", PrimitiveType.Cylinder, null, from, new Vector3(0.22f, 0.02f, 0.22f), MaterialLibrary.Get(CoinColor));
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var coin = go.AddComponent<CoinPickup>();
            coin._amount = amount;
            coin._reason = reason;
            coin._collector = collector;
            coin._start = from;
            coin._landing = new Vector3(landing.x, RestHeight, landing.z);
            coin._arcHeight = Random.Range(0.8f, 1.6f);
            return coin;
        }

        private void OnEnable()
        {
            GameEvents.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            GameEvents.DayEnded -= OnDayEnded;
        }

        private void Update()
        {
            if (_collected) return;
            _age += Time.deltaTime;
            transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);

            if (!_landed)
            {
                float t = Mathf.Clamp01(_age / FlightSeconds);
                Vector3 flat = Vector3.Lerp(_start, _landing, t);
                float lift = Mathf.Sin(t * Mathf.PI) * _arcHeight;
                transform.position = new Vector3(flat.x, Mathf.Lerp(_start.y, _landing.y, t) + lift, flat.z);
                if (t >= 1f) _landed = true;
                return;
            }

            if (_collector == null) return;
            Vector3 target = _collector.position;
            target.y = RestHeight;
            Vector3 toCollector = target - transform.position;
            float distance = toCollector.magnitude;
            if (distance <= CollectRadius)
            {
                Collect();
                return;
            }
            if (distance <= MagnetRadius)
            {
                float pull = MagnetSpeed * (1.4f - distance / MagnetRadius);
                transform.position += toCollector.normalized * (pull * Time.deltaTime);
            }
        }

        private void Collect()
        {
            _collected = true;
            GameEvents.RaiseMoneyRecovered(_amount, _reason, transform.position);
            Destroy(gameObject);
        }

        private void OnDayEnded(int day)
        {
            // Floor items clear at the day transition (notes: cleaning). Uncollected coins are gone.
            if (!_collected) Destroy(gameObject);
        }
    }
}
