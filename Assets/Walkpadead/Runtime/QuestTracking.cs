using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;

namespace Walkpadead.Game
{
    public struct QuestTrackingFrame
    {
        public Pose Head, RightAim;
        public bool HeadTracked, RightTracked, LeftTracked, UserPresent;
        public float Trigger, Reload, Pause, Start, Bonuses;
    }

    public interface IQuestTrackingSource : IDisposable
    {
        QuestTrackingFrame Read();
    }

    /// <summary>OpenXR supplies fused position/orientation, including controller gyro tracking.</summary>
    public sealed class OpenXrQuestTrackingSource : IQuestTrackingSource
    {
        private readonly InputActionAsset asset;
        private readonly InputAction headPosition, headRotation, headTracked, headState;
        private readonly InputAction aimPosition, aimRotation, aimTracked, aimState, leftTracked;
        private readonly InputAction trigger, reload, pause, start, bonuses, click;
        private readonly List<InputActionReference> references = new List<InputActionReference>();

        public OpenXrQuestTrackingSource()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Quest"); asset.AddActionMap(map);
            InputAction Action(string name, string path, string type = "Axis")
            {
                var action = map.AddAction(name, InputActionType.PassThrough, path, expectedControlLayout: type);
                // OpenXR can already be tracking before this scene enables its actions.
                // Read that state even when isTracked/trackingState do not change again.
                action.wantsInitialStateCheck = true;
                return action;
            }
            headPosition = Action("Head position", "<XRHMD>/centerEyePosition", "Vector3");
            headRotation = Action("Head rotation", "<XRHMD>/centerEyeRotation", "Quaternion");
            headTracked = Action("Head tracked", "<XRHMD>/isTracked", "Button");
            headState = Action("Head tracking state", "<XRHMD>/trackingState", "Integer");
            // Aim pose differs from the grip pose. Both the gun and UI use this same ray.
            aimPosition = Action("Right aim position", "<XRController>{RightHand}/pointerPosition", "Vector3");
            aimRotation = Action("Right aim rotation", "<XRController>{RightHand}/pointerRotation", "Quaternion");
            aimTracked = Action("Right aim tracked", "<XRController>{RightHand}/pointer/isTracked", "Button");
            aimState = Action("Right aim tracking state", "<XRController>{RightHand}/pointer/trackingState", "Integer");
            leftTracked = Action("Left tracked", "<XRController>{LeftHand}/isTracked", "Button");
            trigger = Action("Fire", "<XRController>{RightHand}/trigger");
            reload = Action("Reload A", "<XRController>{RightHand}/primaryButton", "Button");
            pause = Action("Pause B", "<XRController>{RightHand}/secondaryButton", "Button");
            start = Action("Start X", "<XRController>{LeftHand}/primaryButton", "Button");
            bonuses = Action("Bonuses Y", "<XRController>{LeftHand}/secondaryButton", "Button");
            click = Action("UI click", "<XRController>{RightHand}/triggerPressed", "Button");
            // A held trigger must not select a menu item when the UI becomes active.
            click.wantsInitialStateCheck = false;
            asset.Enable();
        }

        internal string DescribeTracking() =>
            $"head={headTracked.ReadValue<float>():0}/{headState.ReadValue<int>()} " +
            $"aim={aimTracked.ReadValue<float>():0}/{aimState.ReadValue<int>()} " +
            $"bindings(head/aim/fire)={headTracked.controls.Count}/{aimTracked.controls.Count}/{trigger.controls.Count}";

        internal string DescribeDevices()
        {
            var devices = new System.Text.StringBuilder();
            foreach (var device in InputSystem.devices)
                devices.Append(device.layout).Append('[').Append(string.Join(",", device.usages)).Append("] ");
            return devices.ToString();
        }

        public QuestTrackingFrame Read()
        {
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var present = !head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out var worn) || worn;
            return new QuestTrackingFrame
            {
                Head = new Pose(headPosition.ReadValue<Vector3>(), headRotation.ReadValue<Quaternion>()),
                RightAim = new Pose(aimPosition.ReadValue<Vector3>(), aimRotation.ReadValue<Quaternion>()),
                HeadTracked = headTracked.ReadValue<float>() > 0.5f && (headState.ReadValue<int>() & 3) == 3,
                RightTracked = aimTracked.ReadValue<float>() > 0.5f && (aimState.ReadValue<int>() & 3) == 3,
                LeftTracked = leftTracked.ReadValue<float>() > 0.5f,
                UserPresent = present,
                Trigger = trigger.ReadValue<float>(), Reload = reload.ReadValue<float>(), Pause = pause.ReadValue<float>(),
                Start = start.ReadValue<float>(), Bonuses = bonuses.ReadValue<float>()
            };
        }

        public void BindUI(InputSystemUIInputModule module, Transform origin)
        {
            InputActionReference Reference(InputAction action)
            {
                var reference = InputActionReference.Create(action); references.Add(reference); return reference;
            }
            module.actionsAsset = asset;
            module.point = null; module.move = null; module.submit = null; module.cancel = null;
            module.rightClick = null; module.middleClick = null; module.scrollWheel = null;
            module.trackedDevicePosition = Reference(aimPosition);
            module.trackedDeviceOrientation = Reference(aimRotation);
            module.leftClick = Reference(click);
            module.xrTrackingOrigin = origin;
        }

        public void Dispose()
        {
            asset.Disable();
            foreach (var reference in references) UnityEngine.Object.Destroy(reference);
            UnityEngine.Object.Destroy(asset);
        }
    }
}
