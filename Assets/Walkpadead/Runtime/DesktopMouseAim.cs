using UnityEngine;
using UnityEngine.InputSystem;

namespace Walkpadead.Game
{
    public interface IAimSource
    {
        Ray AimRay { get; }
        bool FirePressed { get; }
        bool ReloadPressed { get; }
        void Tick();
    }

    public sealed class DesktopMouseAim : IAimSource
    {
        private readonly Camera camera;
        private float yaw;
        private float pitch;
        public DesktopMouseAim(Camera camera) { this.camera = camera; }
        public Ray AimRay => new Ray(camera.transform.position, camera.transform.forward);
        public bool FirePressed => Mouse.current?.leftButton.wasPressedThisFrame == true;
        public bool ReloadPressed => Keyboard.current?.rKey.wasPressedThisFrame == true;

        public void Tick()
        {
            var delta = Mouse.current?.delta.ReadValue() ?? Vector2.zero;
            // Mouse delta is already a per-frame displacement: do not multiply by dt.
            yaw = Mathf.Clamp(yaw + delta.x * 0.085f, -65, 65);
            pitch = Mathf.Clamp(pitch - delta.y * 0.085f, -40, 45);
            camera.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
        }
    }
}
