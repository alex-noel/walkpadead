using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Walkpadead.Core;
using Walkpadead.Game;

namespace Walkpadead.Tests
{
    public sealed class QuestTests
    {
        private Scene scene;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("QuestPrototype");
            SceneManager.SetActiveScene(scene);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        { if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene); }

        [Test]
        public void HeadYawSteersAtFullReportedSpeedRegardlessOfPitch()
        {
            var rig = new GameObject("Rig").AddComponent<ForwardLocomotion>();
            rig.Advance(3.0 / 3.6, Quaternion.Euler(60, 90, 0) * Vector3.forward);
            Assert.That(rig.transform.position.x, Is.EqualTo(3.0 / 3.6).Within(0.00001));
            Assert.That(rig.transform.position.y, Is.Zero);
            Assert.That(rig.transform.position.z, Is.EqualTo(0).Within(0.00001));
            rig.Advance(1, Vector3.up);
            Assert.That(rig.transform.position.x, Is.EqualTo(1 + 3.0 / 3.6).Within(0.00001), "Vertical gaze retains last heading");
            rig.Advance(1, Vector3.back);
            Assert.That(rig.transform.position.z, Is.EqualTo(-1).Within(0.00001));
        }

        [Test]
        public void OpenXrBindingsReadHeadPoseRightAimPoseAndTouchButtons()
        {
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            var head = InputSystem.AddDevice<XRHMD>();
            var right = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            var left = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            using (var source = new OpenXrQuestTrackingSource())
            {
                try
                {
                    SetControl(head.isTracked, 1f); SetControl(head.trackingState, 3);
                    SetControl(head.centerEyePosition, new Vector3(0, 1.6f, 0));
                    SetControl(head.centerEyeRotation, Quaternion.Euler(0, 80, 0));
                    SetControl(right.pointer.isTracked, 1f); SetControl(right.pointer.trackingState, 3);
                    SetControl(right.pointerPosition, new Vector3(0.3f, 1.2f, 0.4f));
                    SetControl(right.pointerRotation, Quaternion.Euler(0, -20, 0));
                    SetControl(right.trigger, 0.9f); SetControl(right.primaryButton, 1f);
                    SetControl(left.isTracked, 1f); SetControl(left.primaryButton, 1f);
                    var frame = source.Read();
                    Assert.That(frame.HeadTracked && frame.RightTracked && frame.LeftTracked, Is.True);
                    Assert.That(frame.Head.position.y, Is.EqualTo(1.6f).Within(0.001));
                    Assert.That(Quaternion.Angle(frame.RightAim.rotation, Quaternion.Euler(0, -20, 0)), Is.LessThan(0.01));
                    Assert.That(frame.Trigger, Is.EqualTo(0.9f).Within(0.001));
                    Assert.That(frame.Reload, Is.EqualTo(1)); Assert.That(frame.Start, Is.EqualTo(1));
                }
                finally { InputSystem.RemoveDevice(head); InputSystem.RemoveDevice(right); InputSystem.RemoveDevice(left); }
            }
        }

        [Test]
        public void DevicesAlreadyTrackingBeforeActionsEnableAreRecognized()
        {
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            var head = InputSystem.AddDevice<XRHMD>();
            var right = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            try
            {
                SetControl(head.isTracked, 1f); SetControl(head.trackingState, 3);
                SetControl(head.centerEyePosition, new Vector3(0, 1.6f, 0));
                SetControl(head.centerEyeRotation, Quaternion.identity);
                SetControl(right.pointer.isTracked, 1f); SetControl(right.pointer.trackingState, 3);
                SetControl(right.pointerPosition, new Vector3(0.3f, 1.2f, 0.4f));
                SetControl(right.pointerRotation, Quaternion.identity);
                SetControl(right.trigger, 1f);
                using (var source = new OpenXrQuestTrackingSource())
                {
                    InputSystem.Update();
                    var frame = source.Read();
                    Assert.That(frame.HeadTracked, Is.True, "The HMD was already tracking when actions were enabled");
                    Assert.That(frame.RightTracked, Is.True, "An already-tracked controller must not require tracking loss to activate");
                    Assert.That(frame.Trigger, Is.EqualTo(1), "Read the held trigger so the release gate can suppress it");
                    Assert.That(new ReleaseToPress().Read(frame.Trigger, true), Is.False);
                }
            }
            finally { InputSystem.RemoveDevice(head); InputSystem.RemoveDevice(right); }
        }

        [Test]
        public void TriggerRequiresReleaseAfterTrackingLossAndRejectsInvalidInput()
        {
            var button = new ReleaseToPress();
            Assert.That(button.Read(1, true), Is.False);
            Assert.That(button.Read(0, true), Is.False);
            Assert.That(button.Read(0.8f, true), Is.True);
            Assert.That(button.Read(1, true), Is.False);
            button.Read(0, true); button.Read(0, false);
            Assert.That(button.Read(1, true), Is.False);
            button.Read(0, true); button.Read(float.NaN, true);
            Assert.That(button.Read(1, true), Is.False);
            button.Read(0.2f, true);
            Assert.That(button.Read(0.8f, true), Is.True);
        }

        [UnityTest]
        public IEnumerator QuestSteersFromHeadButShootsFromControllerAndReloads()
        {
            var game = new GameObject("Quest game").AddComponent<DesktopGame>();
            var tracking = new Poses(); game.Quest.UseTrackingSource(tracking); game.UseSimulator();
            yield return null;
            Assert.That(game.IsQuest && game.ControlsReady, Is.True);
            Assert.That(game.GetComponentInChildren<Canvas>(), Is.Null); // HUD belongs to the tracked camera.
            var hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHud>();
            Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(hud.GetComponent<Canvas>().worldCamera, Is.EqualTo(game.ViewCamera));
            tracking.Frame.Head.rotation = Quaternion.Euler(45, 90, 0);
            tracking.Frame.Start = 1;
            yield return null; yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(game.PlayerPosition.x, Is.GreaterThan(0));
            Assert.That(game.PlayerPosition.z, Is.EqualTo(0).Within(0.0001));
            Assert.That(game.Session.SpeedMps, Is.EqualTo(3.0 / 3.6).Within(0.000001));
            Assert.That(Vector3.Dot(game.Quest.AimRay.direction, Vector3.forward), Is.GreaterThan(0.999));
            var target = ZombieActor.Spawn(new Vector3(0, 0, 5), null);
            var controllerWorld = game.Quest.RightAim.position;
            tracking.Frame.RightAim.rotation = Quaternion.LookRotation(target.Chest - controllerWorld);
            tracking.Frame.Trigger = 0; yield return null;
            tracking.Frame.Trigger = 1;
            yield return new WaitForSeconds(0.25f);
            // Fire suppression on Start deliberately requires a new press after the menu closes.
            tracking.Frame.Trigger = 0; yield return null;
            var ammo = game.Gun.Ammo;
            tracking.Frame.Trigger = 1; yield return null;
            Assert.That(game.Gun.Ammo, Is.EqualTo(ammo - 1));
            yield return null;
            Assert.That(game.Gun.Ammo, Is.EqualTo(ammo - 1), "Holding the trigger does not repeatedly fire");
            tracking.Frame.Reload = 1; yield return null;
            Assert.That(game.Gun.Reloading, Is.True);
        }

        [UnityTest]
        public IEnumerator TrackedControllerPointerCanStartFromTheWorldSpaceMenu()
        {
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>();
            var head = InputSystem.AddDevice<XRHMD>();
            var right = InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            try
            {
                var game = new GameObject("Quest pointer game").AddComponent<DesktopGame>();
                game.UseSimulator();
                SetControl(head.isTracked, 1f); SetControl(head.trackingState, 3);
                SetControl(head.centerEyePosition, Vector3.zero); SetControl(head.centerEyeRotation, Quaternion.identity);
                SetControl(right.pointer.isTracked, 1f); SetControl(right.pointer.trackingState, 3);
                var hand = new Vector3(0.25f, -0.25f, 0.3f);
                SetControl(right.pointerPosition, hand); SetControl(right.pointerRotation, Quaternion.identity);
                yield return null;
                Assert.That(game.ControlsReady, Is.True);
                var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Start run");
                var rect = (RectTransform)button.transform;
                var target = game.Quest.TrackingOrigin.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                SetControl(right.pointerRotation, Quaternion.LookRotation(target - hand));
                SetControl(right.trigger, 0f); SetControl(right.triggerPressed, 0f); yield return null;
                SetControl(right.trigger, 1f); SetControl(right.triggerPressed, 1f); yield return null;
                SetControl(right.trigger, 0f); SetControl(right.triggerPressed, 0f); yield return null; yield return null;
                Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Running));
                Assert.That(game.Gun.Ammo, Is.EqualTo(Sidearm.MagazineSize), "Selecting Start must not also fire the gun");
            }
            finally { InputSystem.RemoveDevice(head); InputSystem.RemoveDevice(right); }
        }

        [UnityTest]
        public IEnumerator TrackingLossFreezesGameAndRequiresExplicitResumeAndNewTriggerPress()
        {
            var game = new GameObject("Quest game").AddComponent<DesktopGame>();
            var tracking = new Poses(); game.Quest.UseTrackingSource(tracking); game.UseSimulator();
            yield return null; game.StartOrResume(); yield return null;
            tracking.Frame.RightTracked = false; tracking.Frame.Trigger = 1;
            yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Paused));
            var position = game.PlayerPosition; var ammo = game.Gun.Ammo;
            tracking.Frame.RightTracked = true; yield return null; yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Paused));
            Assert.That(game.PlayerPosition, Is.EqualTo(position));
            game.StartOrResume(); yield return new WaitForSeconds(0.3f);
            Assert.That(game.Gun.Ammo, Is.EqualTo(ammo), "Held trigger cannot fire on tracking recovery");
            tracking.Frame.Trigger = 0; yield return null;
            tracking.Frame.Trigger = 1; yield return null;
            Assert.That(game.Gun.Ammo, Is.EqualTo(ammo - 1));
            tracking.Frame.UserPresent = false; yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Paused));
            game.StartOrResume(); Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Paused));
        }

        [Test]
        public void QuestPickupsAndZombiesUsePlanarDistanceAfterTurning()
        {
            var pickup = SpeedPickup.Spawn(new Vector3(8, 0, 0), -1, null);
            Assert.That(pickup.Advance(new Vector3(0, 0, 9), 0), Is.False);
            Assert.That(pickup.Advance(new Vector3(8, 0, 0), 0), Is.True);
            Assert.That(pickup.Advance(new Vector3(8, 0, 0), 0), Is.False);
            var zombie = ZombieActor.Spawn(new Vector3(5, 0, -2), null);
            Assert.That(zombie.Tick(1, Vector3.zero, true), Is.False, "Passing world Z alone cannot damage the player");
            Assert.That(zombie.transform.position.magnitude, Is.LessThan(new Vector3(5, 0, -2).magnitude));
        }

        private static void SetControl<T>(InputControl<T> control, T value) where T : struct
        {
            using (StateEvent.From(control.device, out var eventPtr))
            {
                control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();
        }

        private sealed class Poses : IQuestTrackingSource
        {
            public QuestTrackingFrame Frame = new QuestTrackingFrame {
                Head = new Pose(Vector3.zero, Quaternion.identity),
                RightAim = new Pose(new Vector3(0.25f, -0.25f, 0.3f), Quaternion.identity),
                HeadTracked = true, RightTracked = true, LeftTracked = true, UserPresent = true
            };
            public QuestTrackingFrame Read() => Frame;
            public void Dispose() { }
        }
    }
}
