using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;
using Walkpadead.Core;

namespace Walkpadead.Game
{
    public sealed class QuestControls : MonoBehaviour, IAimSource
    {
        public Transform TrackingOrigin { get; private set; }
        public Transform RightAim { get; private set; }
        public bool TrackingReady { get; private set; }
        public string Status { get; private set; } = "Waiting for headset and right controller";
        public bool FirePressed { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool StartPressed { get; private set; }
        public bool BonusesPressed { get; private set; }
        public Ray AimRay => new Ray(RightAim.position, RightAim.forward);
        private Camera view;
        private IQuestTrackingSource source;
        private readonly ReleaseToPress fire = new ReleaseToPress(), reload = new ReleaseToPress(),
            pause = new ReleaseToPress(), start = new ReleaseToPress(), bonuses = new ReleaseToPress();
        private bool focused = true, suspended, centered;
        private LineRenderer pointer;
        private InputSystemUIInputModule ui;
        private readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        private XRInputSubsystem inputSubsystem;
        private bool floorOrigin;
        private float nextDiagnosticAt;
        private string lastDiagnosticStatus;

        public void Build(Camera camera, Transform player)
        {
            view = camera;
            TrackingOrigin = new GameObject("XR tracking origin").transform;
            TrackingOrigin.SetParent(player, false);
            TrackingOrigin.localPosition = Vector3.up * 1.65f;
            view.transform.SetParent(TrackingOrigin, false); view.transform.localPosition = Vector3.zero;
            RightAim = new GameObject("Right controller aim").transform;
            RightAim.SetParent(TrackingOrigin, false);
            source = new OpenXrQuestTrackingSource();
            pointer = new GameObject("Controller aim laser").AddComponent<LineRenderer>();
            pointer.transform.SetParent(RightAim, false);
            pointer.sharedMaterial = PrototypeVisuals.Material("BonusUp");
            pointer.positionCount = 2; pointer.startWidth = 0.0025f; pointer.endWidth = 0.001f;
            pointer.gameObject.layer = 2;
            Application.onBeforeRender += BeforeRender;
        }

        /// <summary>Supports deterministic pose replays as well as OpenXR input.</summary>
        public void UseTrackingSource(IQuestTrackingSource next)
        {
            if (next == null) throw new System.ArgumentNullException(nameof(next));
            if (ui != null) ui.enabled = false;
            source?.Dispose(); source = next; centered = false; ResetButtons(); TrackingReady = false;
        }

        public void BindUI(InputSystemUIInputModule module)
        {
            ui = module;
            if (source is OpenXrQuestTrackingSource openXr) openXr.BindUI(module, TrackingOrigin);
        }

        public void Tick()
        {
            ConfigureOrigin();
            var frame = source.Read();
            ApplyPoses(frame);
            TrackingReady = focused && !suspended && frame.UserPresent &&
                frame.HeadTracked && frame.RightTracked && Valid(frame.Head) && Valid(frame.RightAim);
            Status = !focused || suspended ? "Quest app interrupted · press X to resume" : !frame.UserPresent ?
                "Put on the headset to resume" : !frame.HeadTracked ? "Headset tracking lost" :
                !frame.RightTracked ? "Right controller tracking lost" : "Quest tracking ready";
            FirePressed = fire.Read(frame.Trigger, TrackingReady);
            ReloadPressed = reload.Read(frame.Reload, TrackingReady);
            PausePressed = pause.Read(frame.Pause, TrackingReady);
            StartPressed = start.Read(frame.Start, TrackingReady && frame.LeftTracked);
            BonusesPressed = bonuses.Read(frame.Bonuses, TrackingReady && frame.LeftTracked);
            LogDiagnostics(frame);
            if (ui != null) ui.enabled = TrackingReady && source is OpenXrQuestTrackingSource;
            RightAim.gameObject.SetActive(TrackingReady);
            if (TrackingReady)
            {
                pointer.SetPosition(0, AimRay.GetPoint(0.2f));
                var distance = Physics.Raycast(AimRay, out var hit, 30, ~(1 << 2)) ? hit.distance : 30;
                pointer.SetPosition(1, AimRay.GetPoint(distance));
            }
        }

        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogDiagnostics(QuestTrackingFrame frame)
        {
            if (source is OpenXrQuestTrackingSource openXr &&
                (Status != lastDiagnosticStatus || Time.unscaledTime >= nextDiagnosticAt))
            {
                if (Status != lastDiagnosticStatus)
                    Debug.Log("[Walkpadead Quest] devices " + openXr.DescribeDevices());
                Debug.Log($"[Walkpadead Quest] {Status}; ready={TrackingReady} focused={focused} " +
                    $"paused={suspended} present={frame.UserPresent} left={frame.LeftTracked}; " +
                    openXr.DescribeTracking() + $" trigger={frame.Trigger:0.00}");
                lastDiagnosticStatus = Status; nextDiagnosticAt = Time.unscaledTime + 5;
            }
            if (FirePressed || ReloadPressed || PausePressed || StartPressed || BonusesPressed)
                Debug.Log($"[Walkpadead Quest] buttons fire={FirePressed} reload={ReloadPressed} " +
                    $"pause={PausePressed} start={StartPressed} bonuses={BonusesPressed}");
        }

        public void ResetButtons()
        {
            fire.Reset(); reload.Reset(); pause.Reset(); start.Reset(); bonuses.Reset();
            FirePressed = ReloadPressed = PausePressed = StartPressed = BonusesPressed = false;
        }

        private void ConfigureOrigin()
        {
            if (inputSubsystem != null && inputSubsystem.running) return;
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var subsystem in subsystems)
            {
                if (!subsystem.running) continue;
                inputSubsystem = subsystem;
                floorOrigin = subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                if (!floorOrigin) subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                centered = false;
                break;
            }
        }

        private void BeforeRender()
        {
            if (source != null && focused && !suspended) ApplyPoses(source.Read());
        }

        private void ApplyPoses(QuestTrackingFrame frame)
        {
            if (frame.HeadTracked && Valid(frame.Head))
            {
                if (!centered)
                {
                    TrackingOrigin.localPosition = new Vector3(-frame.Head.position.x,
                        floorOrigin ? 0 : 1.65f, -frame.Head.position.z);
                    centered = true;
                }
                view.transform.SetLocalPositionAndRotation(frame.Head.position, frame.Head.rotation);
            }
            if (frame.RightTracked && Valid(frame.RightAim))
                RightAim.SetLocalPositionAndRotation(frame.RightAim.position, frame.RightAim.rotation);
        }

        private static bool Valid(Pose pose) => float.IsFinite(pose.position.sqrMagnitude) &&
            float.IsFinite(pose.rotation.x) && float.IsFinite(pose.rotation.y) &&
            float.IsFinite(pose.rotation.z) && float.IsFinite(pose.rotation.w) &&
            Quaternion.Dot(pose.rotation, pose.rotation) > 0.5f;

        private void OnApplicationFocus(bool hasFocus)
        { focused = hasFocus; if (!hasFocus) { TrackingReady = false; ResetButtons(); } }
        private void OnApplicationPause(bool paused)
        { suspended = paused; if (paused) { TrackingReady = false; ResetButtons(); } }
        private void OnDestroy()
        { Application.onBeforeRender -= BeforeRender; source?.Dispose(); }
    }
}
