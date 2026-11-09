using UnityEngine;
using UnityEngine.Rendering;

namespace Walkpadead.Game
{
    /// <summary>An open extraction yard that can be traversed in any gaze direction.</summary>
    public sealed class QuestArena : MonoBehaviour
    {
        private const float Size = 48;
        private readonly Transform[] tiles = new Transform[25];
        public void Build()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.42f, 0.46f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f; RenderSettings.fogColor = new Color(0.055f, 0.10f, 0.12f);
            var sun = new GameObject("Yard daylight").AddComponent<Light>();
            sun.transform.SetParent(transform, false); sun.type = LightType.Directional;
            sun.intensity = 1.2f; sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = new GameObject("Yard tile").transform; tile.SetParent(transform, false); tiles[i] = tile;
                PrototypeVisuals.Part("Ground", PrimitiveType.Cube, tile, Vector3.down * 0.15f,
                    new Vector3(Size, 0.3f, Size), "Asphalt");
                for (var line = -18; line <= 18; line += 12)
                {
                    PrototypeVisuals.Part("East marking", PrimitiveType.Cube, tile, new Vector3(0, 0.005f, line),
                        new Vector3(Size, 0.01f, 0.06f), "Paint");
                    PrototypeVisuals.Part("North marking", PrimitiveType.Cube, tile, new Vector3(line, 0.006f, 0),
                        new Vector3(0.06f, 0.01f, Size), "Paint");
                }
            }
            Follow(Vector3.zero);
        }
        public void Follow(Vector3 player)
        {
            var x = Mathf.Round(player.x / Size) * Size;
            var z = Mathf.Round(player.z / Size) * Size;
            for (var i = 0; i < tiles.Length; i++)
                tiles[i].position = new Vector3(x + (i % 5 - 2) * Size, 0, z + (i / 5 - 2) * Size);
        }
    }
}
