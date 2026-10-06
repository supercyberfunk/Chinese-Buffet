using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace BuffetSim.Models
{
    /// <summary>
    /// Loads a .glb from StreamingAssets with com.unity.cloud.gltfast and swaps out the placeholder
    /// primitives when it succeeds. If the file is missing, the placeholder stays, so the demo
    /// works with zero art. Drop files into Assets/StreamingAssets/Models/ (player.glb, customer.glb).
    /// </summary>
    public sealed class GlbModelLoader : MonoBehaviour
    {
        [Tooltip("Path relative to Assets/StreamingAssets, e.g. Models/customer.glb")]
        [SerializeField] private string streamingAssetsPath;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private Vector3 modelScale = Vector3.one;

        private GltfImport _gltf;

        public bool Loaded { get; private set; }

        public void Configure(string relativePath, GameObject placeholderVisual, Vector3? scale = null)
        {
            streamingAssetsPath = relativePath;
            placeholder = placeholderVisual;
            if (scale.HasValue) modelScale = scale.Value;
        }

        private async void Start()
        {
            try
            {
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Buffet] Failed to load GLB '{streamingAssetsPath}': {ex.Message}");
            }
        }

        private async Task LoadAsync()
        {
            if (string.IsNullOrEmpty(streamingAssetsPath)) return;
            string fullPath = Path.Combine(Application.streamingAssetsPath, streamingAssetsPath);
            if (!File.Exists(fullPath)) return;

            // GltfImport owns the meshes/textures it creates; it is disposed with this component.
            _gltf = new GltfImport();
            bool ok = await _gltf.Load(new Uri(fullPath).AbsoluteUri);
            if (this == null) return;
            if (!ok)
            {
                DisposeImport();
                return;
            }

            var modelRoot = new GameObject("GLB Model");
            modelRoot.transform.SetParent(transform, false);
            modelRoot.transform.localScale = modelScale;
            ok = await _gltf.InstantiateMainSceneAsync(modelRoot.transform);
            if (this == null) return;
            if (!ok)
            {
                Destroy(modelRoot);
                DisposeImport();
                return;
            }

            Loaded = true;
            if (placeholder != null) placeholder.SetActive(false);
        }

        private void OnDestroy()
        {
            DisposeImport();
        }

        private void DisposeImport()
        {
            _gltf?.Dispose();
            _gltf = null;
        }
    }
}
