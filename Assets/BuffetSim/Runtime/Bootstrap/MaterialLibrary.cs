using System.Collections.Generic;
using UnityEngine;

namespace BuffetSim.Bootstrap
{
    /// <summary>
    /// Hands out one shared material per colour so the placeholder scene doesn't allocate a
    /// material per primitive. Uses a base material asset when the bootstrap provides one
    /// (so builds include the shader), otherwise finds a lit shader at runtime.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        private static Material _baseMaterial;
        private static Shader _shader;

        public static void Initialize(Material baseMaterial)
        {
            Clear();
            _baseMaterial = baseMaterial;
        }

        public static void Clear()
        {
            Cache.Clear();
            _shader = null;
        }

        public static Material Get(Color color)
        {
            if (Cache.TryGetValue(color, out Material cached) && cached != null) return cached;

            Material material = _baseMaterial != null ? new Material(_baseMaterial) : new Material(FindShader());
            material.color = color;
            material.name = $"Buffet {ColorUtility.ToHtmlStringRGB(color)}";
            Cache[color] = material;
            return material;
        }

        private static Shader FindShader()
        {
            if (_shader != null) return _shader;
            string[] candidates = { "Standard", "Universal Render Pipeline/Lit", "HDRP/Lit", "Legacy Shaders/Diffuse" };
            foreach (string name in candidates)
            {
                Shader shader = Shader.Find(name);
                if (shader != null)
                {
                    _shader = shader;
                    return _shader;
                }
            }
            _shader = Shader.Find("Sprites/Default");
            return _shader;
        }
    }
}
