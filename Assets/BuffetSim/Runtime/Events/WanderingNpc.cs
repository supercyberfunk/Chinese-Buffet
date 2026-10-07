using System;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// Drives a NavMeshAgent between random reachable points inside a box, pausing 0.3-1.2 s between
    /// legs (rats, thieves, anyone with nowhere in particular to be). <see cref="GoTo"/> interrupts
    /// wandering for one trip with a callback; wandering resumes afterwards if it was on. A trip that
    /// cannot be completed (no path, blocked) still reports arrival after a timeout so callers never hang.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WanderingNpc : MonoBehaviour
    {
        private const float MinPause = 0.3f;
        private const float MaxPause = 1.2f;
        private const float LegSampleRadius = 2f;
        private const int LegAttempts = 8;
        private const float RetryPause = 1f;

        private NavMeshAgent _agent;
        private NavMeshPath _path;
        private Bounds _bounds;
        private System.Random _rng;
        private bool _wandering;
        private bool _legActive;
        private float _legTimeout;
        private float _pauseTimer;
        private bool _travelling;
        private float _travelTimeout;
        private Action _onArrive;

        public bool IsWandering => _wandering;
        public bool IsTravelling => _travelling;

        private void Awake()
        {
            EnsureAgent();
        }

        /// <summary>Starts wandering inside <paramref name="area"/> at <paramref name="speed"/>.</summary>
        public void Configure(Bounds area, float speed, System.Random rng)
        {
            EnsureAgent();
            _bounds = area;
            _rng = rng ?? new System.Random();
            _agent.speed = speed;
            _wandering = true;
            _legActive = false;
            _pauseTimer = 0f;
        }

        /// <summary>Stops in place: no more legs, and a pending <see cref="GoTo"/> is dropped without its callback.</summary>
        public void StopWandering()
        {
            EnsureAgent();
            _wandering = false;
            _legActive = false;
            _travelling = false;
            _onArrive = null;
            Halt();
        }

        /// <summary>
        /// One trip to <paramref name="destination"/> (snapped to the NavMesh). <paramref name="onArrive"/>
        /// runs once, on arrival or when the trip times out; wandering, if on, resumes after it.
        /// </summary>
        public void GoTo(Vector3 destination, Action onArrive)
        {
            EnsureAgent();
            _onArrive = onArrive;
            _travelling = true;
            _legActive = false;

            if (!EnsureOnNavMesh())
            {
                Arrive();
                return;
            }

            Vector3 target = ChaosActors.SampleNavMesh(destination, 3f);
            _travelTimeout = TimeAllowance(target);
            _agent.isStopped = false;
            if (!_agent.SetDestination(target)) Arrive();
        }

        private void Update()
        {
            if (_agent == null) return;
            float dt = Time.deltaTime;

            if (_travelling)
            {
                _travelTimeout -= dt;
                if (_travelTimeout <= 0f || HasArrived()) Arrive();
                return;
            }

            if (!_wandering) return;

            if (_legActive)
            {
                _legTimeout -= dt;
                if (_legTimeout > 0f && !HasArrived()) return;
                _legActive = false;
                _pauseTimer = MinPause + (float)_rng.NextDouble() * (MaxPause - MinPause);
                return;
            }

            _pauseTimer -= dt;
            if (_pauseTimer > 0f) return;
            PickNextLeg();
        }

        /// <summary>Tries a few random points in the box and takes the first one with a complete path.</summary>
        private void PickNextLeg()
        {
            if (!EnsureOnNavMesh())
            {
                _pauseTimer = RetryPause;
                return;
            }

            if (_path == null) _path = new NavMeshPath();
            for (int i = 0; i < LegAttempts; i++)
            {
                Vector3 candidate = ChaosActors.RandomPointInBounds(_bounds, _rng);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, LegSampleRadius, NavMesh.AllAreas)) continue;
                var flat = new Vector3(hit.position.x, _bounds.center.y, hit.position.z);
                if (!_bounds.Contains(flat)) continue;
                if (!_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;

                _agent.isStopped = false;
                if (_agent.SetPath(_path))
                {
                    _legActive = true;
                    _legTimeout = TimeAllowance(hit.position);
                    return;
                }
            }
            _pauseTimer = RetryPause;
        }

        private void Arrive()
        {
            _travelling = false;
            Action callback = _onArrive;
            _onArrive = null;
            Halt();
            if (_wandering) _pauseTimer = MinPause;
            callback?.Invoke();
        }

        private void Halt()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        private bool HasArrived()
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return true;
            if (_agent.pathPending) return false;
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
            float remaining = _agent.remainingDistance;
            // A partial path with no measurable end: standing still is as far as it goes.
            if (float.IsInfinity(remaining)) return _agent.velocity.sqrMagnitude < 0.05f;
            if (remaining > _agent.stoppingDistance + 0.2f) return false;
            return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.05f;
        }

        private bool EnsureOnNavMesh()
        {
            if (!_agent.enabled) return false;
            if (_agent.isOnNavMesh) return true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                return _agent.Warp(hit.position);
            return false;
        }

        /// <summary>Generous time budget for a trip, so a blocked agent is given up on rather than waited for forever.</summary>
        private float TimeAllowance(Vector3 target)
        {
            float distance = Vector3.Distance(transform.position, target);
            return 4f + distance / Mathf.Max(0.5f, _agent.speed) * 2.5f;
        }

        private void EnsureAgent()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        }
    }
}
