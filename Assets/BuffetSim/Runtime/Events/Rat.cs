using BuffetSim.Core;
using BuffetSim.Interaction;
using BuffetSim.Player;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// One rat from the Rat Summoner event: wanders the dining floor until the player catches it,
    /// then sails out the door in a scripted arc for a small bounty. Tells its runner when it is gone.
    /// </summary>
    public sealed class Rat : MonoBehaviour, IInteractable
    {
        private const float FlightSeconds = 0.8f;
        private const float ArcHeight = 1.6f;
        private const float Bounty = 4f;

        private RatSummonerRunner _owner;
        private Vector3 _tossTarget;
        private NavMeshAgent _agent;
        private WanderingNpc _npc;
        private Collider _collider;
        private TextMesh _label;
        private bool _flying;
        private float _flightAge;
        private Vector3 _flightStart;
        private bool _gone;

        public bool IsFlying => _flying;

        public void Initialize(RatSummonerRunner owner, Vector3 tossTarget, TextMesh label)
        {
            _owner = owner;
            _tossTarget = tossTarget;
            _label = label;
            _agent = GetComponent<NavMeshAgent>();
            _npc = GetComponent<WanderingNpc>();
            _collider = GetComponent<Collider>();
        }

        public string GetPrompt(PlayerInventory inventory)
        {
            return _flying || _gone ? string.Empty : "[E] Catch rat";
        }

        public void Interact(PlayerInventory inventory)
        {
            if (_flying || _gone) return;
            Toss();
        }

        /// <summary>Stops the rat, pays the bounty where the player can see it, and starts the arc out the door.</summary>
        private void Toss()
        {
            _flying = true;
            _flightAge = 0f;
            _flightStart = transform.position;
            if (_npc != null) _npc.StopWandering();
            if (_agent != null) _agent.enabled = false; // the scripted arc owns the transform now
            if (_collider != null) _collider.enabled = false;
            if (_label != null) _label.text = "!";

            GameEvents.RaiseNotice($"Tossed a rat outside. +${Bounty:0.00}");
            GameEvents.RaiseMoneyRecovered(Bounty, "rat bounty", transform.position);
        }

        private void Update()
        {
            if (!_flying || _gone) return;
            _flightAge += Time.deltaTime;
            float t = Mathf.Clamp01(_flightAge / FlightSeconds);
            Vector3 flat = Vector3.Lerp(_flightStart, _tossTarget, t);
            float lift = Mathf.Sin(t * Mathf.PI) * ArcHeight;
            transform.position = new Vector3(flat.x, Mathf.Lerp(_flightStart.y, _tossTarget.y, t) + lift, flat.z);
            transform.Rotate(0f, 0f, 720f * Time.deltaTime, Space.Self);
            if (t >= 1f)
            {
                _gone = true;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            _gone = true;
            // Scene teardown is not a rat leaving: don't let the runner resolve the event on the way out.
            if (!gameObject.scene.isLoaded) return;
            if (_owner != null) _owner.OnRatGone(this);
        }
    }
}
