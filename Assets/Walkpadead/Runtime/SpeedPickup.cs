using UnityEngine;

namespace Walkpadead.Game
{
    public sealed class SpeedPickup : MonoBehaviour
    {
        public int DeltaTenths { get; private set; }
        public bool Collected { get; private set; }
        private Transform token;

        public static SpeedPickup Spawn(float distance, int delta, Transform parent)
            => Spawn(new Vector3(0, 0, distance), delta, parent);

        public static SpeedPickup Spawn(Vector3 position, int delta, Transform parent)
        {
            var root = new GameObject(delta > 0 ? "Speed bonus +0.1" : "Speed bonus -0.1");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var pickup = root.AddComponent<SpeedPickup>(); pickup.DeltaTenths = delta;
            var material = delta > 0 ? "BonusUp" : "BonusDown";
            PrototypeVisuals.Part("Road marker", PrimitiveType.Cylinder, root.transform,
                new Vector3(0, 0.04f, 0), new Vector3(1.25f, 0.035f, 1.25f), material);
            pickup.token = PrototypeVisuals.Part("Pickup", PrimitiveType.Cube, root.transform,
                new Vector3(0, 1.1f, 0), Vector3.one * 0.35f, material).transform;
            var label = new GameObject("Speed change").AddComponent<TextMesh>();
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0, 1.75f, 0);
            label.text = (delta > 0 ? "+0.1" : "-0.1") + " km/h";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.fontSize = 72; label.characterSize = 0.045f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = delta > 0 ? new Color(0.65f, 0.95f, 0.5f) : new Color(1f, 0.65f, 0.3f);
            return pickup;
        }

        public bool Advance(float playerZ, float dt)
            => AdvanceAt(playerZ >= transform.position.z, dt);

        public bool Advance(Vector3 player, float dt)
            => AdvanceAt(Vector3.ProjectOnPlane(player - transform.position, Vector3.up).sqrMagnitude <= 1, dt);

        private bool AdvanceAt(bool reached, float dt)
        {
            if (Collected) return false;
            token.Rotate(0, 55 * dt, 0);
            if (!reached) return false;
            Collected = true; gameObject.SetActive(false);
            return true;
        }
    }
}
