using UnityEngine;

namespace Walkpadead.Game
{
    internal static class PrototypeVisuals
    {
        public static Material Material(string name)
        {
            var material = Resources.Load<Material>("Walkpadead/" + name);
            if (material == null) throw new System.InvalidOperationException(
                "Missing prototype material. Run Walkpadead > Prepare project in the editor.");
            return material;
        }

        public static GameObject Part(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, string material, bool collider = false)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = Material(material);
            var shape = part.GetComponent<Collider>();
            if (!collider && shape != null)
            {
                shape.enabled = false;
                Object.Destroy(shape);
            }
            return part;
        }
    }
}
