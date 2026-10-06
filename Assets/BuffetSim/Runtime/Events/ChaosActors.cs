using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>Small shared helpers for the placeholder actors chaos events spawn (rats, thieves, figures, puddles).</summary>
    internal static class ChaosActors
    {
        /// <summary>The nearest NavMesh point to <paramref name="near"/>, or <paramref name="near"/> itself when none is within <paramref name="maxDistance"/>.</summary>
        public static Vector3 SampleNavMesh(Vector3 near, float maxDistance)
        {
            return NavMesh.SamplePosition(near, out NavMeshHit hit, maxDistance, NavMesh.AllAreas) ? hit.position : near;
        }

        /// <summary>A uniformly random point in the box, at the box's centre height.</summary>
        public static Vector3 RandomPointInBounds(Bounds bounds, System.Random rng)
        {
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, (float)rng.NextDouble());
            float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (float)rng.NextDouble());
            return new Vector3(x, bounds.center.y, z);
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The y of the floor collider under <paramref name="point"/> (NavMesh points can float a few centimetres above it).</summary>
        public static float FloorHeightAt(Vector3 point, float fallback)
        {
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        /// <summary>
        /// An empty root on the Default layer, placed on the NavMesh near <paramref name="near"/>, with a
        /// configured NavMeshAgent. The caller adds visuals, a trigger collider and behaviour.
        /// </summary>
        public static GameObject SpawnAgentRoot(string name, Transform parent, Vector3 near, float radius, float height, float speed, System.Random rng, out NavMeshAgent agent)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = SampleNavMesh(near, 3f);
            root.layer = 0; // Default: the player's interaction ray must be able to hit it.

            agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = speed;
            agent.acceleration = 24f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;
            agent.avoidancePriority = rng != null ? rng.Next(20, 80) : 50;
            return root;
        }
    }
}
