using UnityEngine;
using UnityEngine.Rendering;

namespace Walkpadead.Game
{
    /// <summary>Reusable street sections; all geometry is authored by code.</summary>
    public sealed class RouteVisuals : MonoBehaviour
    {
        private const int Count = 8;
        private const float Length = 24;
        private readonly Transform[] sections = new Transform[Count];

        public void Build()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.37f, 0.43f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.026f;
            RenderSettings.fogColor = new Color(0.055f, 0.10f, 0.12f);
            var sun = new GameObject("Cold dawn").AddComponent<Light>();
            sun.transform.SetParent(transform);
            sun.type = LightType.Directional;
            sun.color = new Color(0.64f, 0.8f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);

            for (var index = 0; index < Count; index++)
            {
                var section = new GameObject("Street section " + index).transform;
                section.SetParent(transform, false);
                section.position = new Vector3(0, 0, (index - 1) * Length);
                sections[index] = section;
                MakeSection(section, index);
            }
        }

        public void Follow(float playerZ)
        {
            foreach (var section in sections)
                if (section.position.z + Length < playerZ - 12)
                    section.position += Vector3.forward * (Count * Length);
        }

        private static void MakeSection(Transform root, int index)
        {
            PrototypeVisuals.Part("Asphalt", PrimitiveType.Cube, root,
                new Vector3(0, -0.2f, 12), new Vector3(12, 0.4f, 24), "Asphalt");
            for (var z = 2; z < 24; z += 6)
            {
                PrototypeVisuals.Part("Lane dash", PrimitiveType.Cube, root,
                    new Vector3(0, 0.008f, z), new Vector3(0.10f, 0.012f, 2.5f), "Paint");
            }
            foreach (var side in new[] { -1, 1 })
            {
                PrototypeVisuals.Part("Curb", PrimitiveType.Cube, root,
                    new Vector3(side * 4.6f, 0.12f, 12), new Vector3(2.8f, 0.24f, 24), "Concrete");
                PrototypeVisuals.Part("Edge stripe", PrimitiveType.Cube, root,
                    new Vector3(side * 3.1f, 0.012f, 12), new Vector3(0.08f, 0.02f, 24), "Paint");
                for (var block = 0; block < 2; block++)
                {
                    var height = 7 + ((index * 13 + block * 7 + side) & 3) * 2;
                    var center = new Vector3(side * 9, height / 2f, 5.5f + block * 12);
                    PrototypeVisuals.Part("Warehouse", PrimitiveType.Cube, root, center,
                        new Vector3(6, height, 10.5f), index % 2 == 0 ? "Building" : "Concrete");
                    for (var row = 0; row < 2; row++)
                    for (var window = 0; window < 3; window++)
                        PrototypeVisuals.Part("Window", PrimitiveType.Cube, root,
                            new Vector3(side * 5.97f, 3.5f + row * 2.7f, center.z - 3 + window * 3),
                            new Vector3(0.04f, 1.2f, 1.4f), (index + row + window) % 4 == 0 ? "WarmLight" : "Window");
                    PrototypeVisuals.Part("Shutter", PrimitiveType.Cube, root,
                        new Vector3(side * 5.94f, 1.45f, center.z), new Vector3(0.06f, 2.65f, 3.2f), "Metal");
                }
                PrototypeVisuals.Part("Streetlight post", PrimitiveType.Cylinder, root,
                    new Vector3(side * 4.4f, 2.7f, 10), new Vector3(0.12f, 2.7f, 0.12f), "Metal");
                PrototypeVisuals.Part("Streetlight arm", PrimitiveType.Cube, root,
                    new Vector3(side * 3.9f, 5.3f, 10), new Vector3(1.2f, 0.1f, 0.1f), "Metal");
                PrototypeVisuals.Part("Lamp", PrimitiveType.Cube, root,
                    new Vector3(side * 3.4f, 5.22f, 10), new Vector3(0.7f, 0.12f, 0.5f), "WarmLight");
                PrototypeVisuals.Part("Road barrier", PrimitiveType.Cube, root,
                    new Vector3(side * 2.9f, 0.45f, 19), new Vector3(0.5f, 0.9f, 2), "Concrete");
                PrototypeVisuals.Part("Barrier reflector", PrimitiveType.Cube, root,
                    new Vector3(side * 2.9f, 0.52f, 17.99f), new Vector3(0.42f, 0.28f, 0.02f), "WarmLight");
            }
        }
    }
}
