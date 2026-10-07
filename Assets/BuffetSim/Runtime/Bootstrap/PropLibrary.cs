using System.Collections.Generic;
using UnityEngine;

namespace BuffetSim.Bootstrap
{
    /// <summary>A placed art prop: its root, scale and world bounds, so the builder can stack things on it.</summary>
    public sealed class PlacedProp
    {
        public GameObject Root;
        public float Scale;
        public Bounds WorldBounds;

        public float Top => WorldBounds.max.y;
        public float Width => WorldBounds.size.x;
        public float Height => WorldBounds.size.y;
        public float Depth => WorldBounds.size.z;
    }

    /// <summary>
    /// Instantiates art models from <c>Assets/Resources/Models/&lt;name&gt;.glb</c> (imported by
    /// com.unity.cloud.gltfast), scales them to a target size and gives them a collider. Returns null
    /// when the model isn't there so the scene builder can fall back to primitives.
    /// </summary>
    public static class PropLibrary
    {
        private const string ResourceFolder = "Models/";
        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        public static bool IsAvailable(string modelName) => LoadPrefab(modelName) != null;

        /// <summary>
        /// Places a model with its bottom-centre at <paramref name="worldPosition"/>. Exactly one of
        /// <paramref name="targetWidth"/> / <paramref name="targetHeight"/> drives a uniform scale
        /// (width wins when both are given); zero for both keeps the authored size.
        /// </summary>
        public static PlacedProp Place(string modelName, Transform parent, Vector3 worldPosition, float yawDegrees = 0f,
            float targetWidth = 0f, float targetHeight = 0f, bool addCollider = true, float colliderHeightFraction = 1f)
        {
            GameObject prefab = LoadPrefab(modelName);
            if (prefab == null) return null;

            var root = new GameObject($"{modelName} (model)");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            root.transform.rotation = Quaternion.identity;

            GameObject instance = Object.Instantiate(prefab, root.transform);
            instance.name = modelName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (!TryGetLocalBounds(root.transform, out Bounds local))
            {
                Debug.LogWarning($"[Buffet] Model '{modelName}' has no renderers; keeping it unscaled.");
                return new PlacedProp { Root = root, Scale = 1f, WorldBounds = new Bounds(worldPosition, Vector3.zero) };
            }

            // Re-pivot so the model stands on its bottom-centre whatever the file's origin was.
            instance.transform.localPosition = new Vector3(-local.center.x, -local.min.y, -local.center.z);
            local.center = new Vector3(0f, local.extents.y, 0f);

            float scale = 1f;
            if (targetWidth > 0f && local.size.x > 0.0001f) scale = targetWidth / local.size.x;
            else if (targetHeight > 0f && local.size.y > 0.0001f) scale = targetHeight / local.size.y;
            root.transform.localScale = Vector3.one * scale;

            if (addCollider)
            {
                // Optionally only the lower part gets a collider (e.g. a counter body but not its sneeze guard),
                // so the player can still look at and interact with things sitting on top.
                float fraction = Mathf.Clamp01(colliderHeightFraction <= 0f ? 1f : colliderHeightFraction);
                float height = local.size.y * fraction;
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(local.center.x, height * 0.5f, local.center.z);
                collider.size = new Vector3(local.size.x, height, local.size.z);
            }

            root.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            TryGetWorldBounds(root.transform, out Bounds world);
            return new PlacedProp { Root = root, Scale = scale, WorldBounds = world };
        }

        private static GameObject LoadPrefab(string modelName)
        {
            if (Cache.TryGetValue(modelName, out GameObject cached) && cached != null) return cached;
            if (Missing.Contains(modelName)) return null;

            GameObject prefab = Resources.Load<GameObject>(ResourceFolder + modelName);
            if (prefab == null)
            {
                Missing.Add(modelName);
                Debug.Log($"[Buffet] No model at Resources/{ResourceFolder}{modelName}; using a placeholder.");
                return null;
            }

            Cache[modelName] = prefab;
            return prefab;
        }

        /// <summary>Bounds of all renderers under <paramref name="root"/>, expressed in root-local space (root must be unscaled and unrotated).</summary>
        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            if (!TryGetWorldBounds(root, out bounds)) return false;
            bounds.center -= root.position;
            return true;
        }

        public static bool TryGetWorldBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }

        public static void Clear()
        {
            Cache.Clear();
            Missing.Clear();
        }
    }
}
