using BuffetSim.Bootstrap;
using UnityEngine;

namespace BuffetSim.UI
{
    /// <summary>A world-space "+$35.00" / "-$15.00" popup that drifts up and fades out.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private TextMesh _text;
        private Color _color;
        private float _life = 1.8f;
        private float _age;
        private float _riseSpeed = 0.9f;

        public static FloatingText Spawn(Vector3 worldPosition, string text, Color color, Font font, float worldHeight = 0.35f)
        {
            var go = new GameObject("Floating Text");
            go.transform.position = worldPosition;
            TextMesh mesh = PrimitiveFactory.Label("Text", go.transform, Vector3.zero, text, worldHeight, font, color);
            mesh.gameObject.AddComponent<Billboard>();
            var floating = go.AddComponent<FloatingText>();
            floating._text = mesh;
            floating._color = color;
            return floating;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);
            if (_text != null)
            {
                float alpha = Mathf.Clamp01(1f - _age / _life);
                _text.color = new Color(_color.r, _color.g, _color.b, alpha);
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
