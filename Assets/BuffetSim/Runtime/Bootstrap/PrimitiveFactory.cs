using UnityEngine;

namespace BuffetSim.Bootstrap
{
    /// <summary>Helpers for building the placeholder level out of Unity primitives.</summary>
    public static class PrimitiveFactory
    {
        /// <summary>A primitive that keeps its collider (walls, counters, tables: things that block and bake into the NavMesh).</summary>
        public static GameObject Solid(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>A primitive with its collider removed immediately, so it neither blocks the player nor bakes into the NavMesh.</summary>
        public static GameObject Visual(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = Solid(name, type, parent, localPosition, localScale, material);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }

        /// <summary>A world-space TextMesh. <paramref name="worldHeight"/> is roughly the height of one line in metres.</summary>
        public static TextMesh Label(string name, Transform parent, Vector3 localPosition, string text, float worldHeight, Font font, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            TextMesh mesh = go.AddComponent<TextMesh>();
            const int fontSize = 64;
            mesh.fontSize = fontSize;
            mesh.characterSize = worldHeight / (fontSize * 0.1f);
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.text = text;
            if (font != null)
            {
                mesh.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return mesh;
        }

        public static Font DefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }
}
