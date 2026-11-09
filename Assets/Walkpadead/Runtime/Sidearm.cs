using System;
using UnityEngine;

namespace Walkpadead.Game
{
    public sealed class Sidearm : MonoBehaviour
    {
        public const int MagazineSize = 12;
        public int Ammo { get; private set; } = MagazineSize;
        public bool Reloading => reloadTime > 0;
        public float HitMarkerTime { get; private set; }
        public event Action<ZombieActor> Killed;
        private float reloadTime;
        private float cooldown;
        private float flashTime;
        private Transform model;
        private GameObject flash;
        private LineRenderer tracer;
        private AudioSource audioSource;
        private AudioClip shotSound;
        private Vector3 restPosition;

        public void Build(Transform mount, bool trackedController = false)
        {
            transform.SetParent(mount, false);
            restPosition = trackedController ? Vector3.zero : new Vector3(0.27f, -0.24f, 0.48f);
            model = new GameObject("Sidearm model").transform;
            model.SetParent(transform, false); model.localPosition = restPosition;
            PrototypeVisuals.Part("Slide", PrimitiveType.Cube, model, Vector3.zero, new Vector3(0.105f, 0.105f, 0.31f), "Gun");
            var grip = PrototypeVisuals.Part("Grip", PrimitiveType.Cube, model,
                new Vector3(0, -0.09f, -0.07f), new Vector3(0.09f, 0.18f, 0.09f), "Metal");
            grip.transform.localRotation = Quaternion.Euler(-14, 0, 0);
            PrototypeVisuals.Part("Sight", PrimitiveType.Cube, model,
                new Vector3(0, 0.065f, 0.11f), new Vector3(0.018f, 0.025f, 0.025f), "WarmLight");
            flash = PrototypeVisuals.Part("Muzzle flash", PrimitiveType.Sphere, model,
                new Vector3(0, 0, 0.25f), new Vector3(0.12f, 0.12f, 0.21f), "Flash");
            flash.SetActive(false);
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            tracer = new GameObject("Tracer").AddComponent<LineRenderer>();
            tracer.transform.SetParent(transform, false);
            tracer.sharedMaterial = PrototypeVisuals.Material("Flash");
            tracer.positionCount = 2; tracer.startWidth = 0.009f; tracer.endWidth = 0.002f;
            tracer.enabled = false;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.volume = 0.13f;
            shotSound = AudioClip.Create("Procedural sidearm", 4410, 1, 44100, false);
            var samples = new float[4410]; var random = new System.Random(71);
            for (var i = 0; i < samples.Length; i++)
                samples[i] = (float)((random.NextDouble() * 2 - 1) * Math.Exp(-i / 510.0));
            shotSound.SetData(samples, 0);
        }

        public void Tick(float dt)
        {
            cooldown = Mathf.Max(0, cooldown - dt);
            HitMarkerTime = Mathf.Max(0, HitMarkerTime - dt);
            if (reloadTime > 0)
            {
                reloadTime = Mathf.Max(0, reloadTime - dt);
                if (reloadTime == 0) Ammo = MagazineSize;
            }
            flashTime = Mathf.Max(0, flashTime - dt);
            flash.SetActive(flashTime > 0);
            tracer.enabled = flashTime > 0;
            model.localPosition = restPosition + new Vector3(0, Reloading ? -0.17f : 0, -cooldown * 0.22f);
        }

        public void Reload()
        {
            if (!Reloading && Ammo < MagazineSize) reloadTime = 1.1f;
        }

        public bool Fire(Ray ray)
        {
            if (Reloading || cooldown > 0) return false;
            if (Ammo <= 0) { Reload(); return false; }
            Ammo--; cooldown = 0.19f; flashTime = 0.045f;
            var end = ray.GetPoint(60);
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out var hit, 60, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var zone = hit.collider.GetComponent<ZombieHitZone>();
                if (zone != null && !zone.Owner.Dead)
                {
                    HitMarkerTime = 0.16f;
                    if (zone.Owner.Hit(zone.Damage)) Killed?.Invoke(zone.Owner);
                }
            }
            tracer.SetPosition(0, model.TransformPoint(new Vector3(0, 0, 0.25f)));
            tracer.SetPosition(1, end);
            audioSource.PlayOneShot(shotSound);
            return true;
        }

        private void OnDestroy() { if (shotSound != null) Destroy(shotSound); }
    }
}
