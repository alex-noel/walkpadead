using UnityEngine;

namespace Walkpadead.Game
{
    public sealed class ZombieActor : MonoBehaviour
    {
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private float walkTime;
        private int health = 2;
        public bool Dead => health <= 0;
        public Vector3 Chest => transform.position + Vector3.up * 1.05f;

        public static ZombieActor Spawn(Vector3 position, Transform parent)
        {
            var root = new GameObject("Approaching infected");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var enemy = root.AddComponent<ZombieActor>();
            enemy.BuildModel();
            return enemy;
        }

        private void BuildModel()
        {
            var torso = PrototypeVisuals.Part("Torn coat", PrimitiveType.Capsule, transform,
                new Vector3(0, 1.04f, 0), new Vector3(0.64f, 0.48f, 0.4f), "Coat", true);
            torso.AddComponent<ZombieHitZone>().Owner = this;
            var head = PrototypeVisuals.Part("Head", PrimitiveType.Sphere, transform,
                new Vector3(0.03f, 1.68f, -0.05f), new Vector3(0.39f, 0.45f, 0.38f), "Skin", true);
            var headZone = head.AddComponent<ZombieHitZone>();
            headZone.Owner = this; headZone.Damage = 2;
            foreach (var side in new[] { -1, 1 })
            {
                PrototypeVisuals.Part("Eye", PrimitiveType.Sphere, transform,
                    new Vector3(side * 0.08f + 0.03f, 1.72f, -0.228f), new Vector3(0.045f, 0.04f, 0.025f), "Eyes");
                var leg = new GameObject("Leg pivot").transform;
                leg.SetParent(transform, false); leg.localPosition = new Vector3(side * 0.17f, 0.72f, 0);
                PrototypeVisuals.Part("Leg", PrimitiveType.Capsule, leg,
                    new Vector3(0, -0.34f, 0), new Vector3(0.23f, 0.37f, 0.25f), "Coat");
                PrototypeVisuals.Part("Boot", PrimitiveType.Cube, leg,
                    new Vector3(0, -0.65f, -0.09f), new Vector3(0.25f, 0.15f, 0.43f), "Metal");
                var arm = new GameObject("Arm pivot").transform;
                arm.SetParent(transform, false); arm.localPosition = new Vector3(side * 0.38f, 1.32f, 0);
                PrototypeVisuals.Part("Arm", PrimitiveType.Capsule, arm,
                    new Vector3(0, -0.26f, -0.15f), new Vector3(0.19f, 0.32f, 0.2f), "Skin");
                if (side < 0) { leftLeg = leg; leftArm = arm; } else { rightLeg = leg; rightArm = arm; }
            }
        }

        /// <returns>True when the enemy reaches the player.</returns>
        public bool Tick(float dt, Vector3 player, bool freeMovement = false)
        {
            if (Dead) return false;
            walkTime += dt * 7;
            var position = transform.position;
            if (freeMovement)
            {
                var destination = new Vector3(player.x, 0, player.z);
                position = Vector3.MoveTowards(position, destination, 0.85f * dt);
                var facing = position - destination; // Procedural model faces local -Z.
                if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
            }
            else
            {
                position.z -= 0.85f * dt;
                position.x = Mathf.MoveTowards(position.x, 0, dt * 0.18f);
            }
            transform.position = position;
            leftLeg.localRotation = Quaternion.Euler(Mathf.Sin(walkTime) * 20, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-Mathf.Sin(walkTime) * 20, 0, 0);
            leftArm.localRotation = Quaternion.Euler(45 + Mathf.Sin(walkTime) * 8, 0, 12);
            rightArm.localRotation = Quaternion.Euler(52 - Mathf.Sin(walkTime) * 8, 0, -12);
            return freeMovement ? Vector3.ProjectOnPlane(position - player, Vector3.up).sqrMagnitude <= 0.64f :
                position.z <= player.z + 0.8f;
        }

        public bool Hit(int damage)
        {
            if (Dead) return false;
            health -= damage;
            if (!Dead) return false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            gameObject.SetActive(false);
            return true;
        }
    }

}
