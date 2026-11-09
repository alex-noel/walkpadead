using System;

namespace Walkpadead.Core
{
    public sealed class InputReading
    {
        public readonly bool Available;
        public readonly double SpeedMps;
        public readonly Guid ConnectionId;
        public readonly long SafetyEpoch;
        public readonly ulong Sequence;
        public readonly bool BeltRunning;
        public readonly string Status;
        public readonly string Source;
        public readonly ulong? DeviceDistanceM;
        public readonly ulong? DeviceElapsedS;
        public readonly ulong? Steps;
        public string SourceLabel => Source switch
        {
            "live_device" => "Live WalkingPad",
            "simulation" => "Simulator",
            "replay" => "Recording",
            _ => "Walking input"
        };

        internal InputReading(bool available, double speed, Guid id, long epoch, string status, TelemetryPacket packet)
        {
            Available = available;
            SpeedMps = available ? speed : 0;
            ConnectionId = id;
            SafetyEpoch = epoch;
            Sequence = packet?.Sequence ?? 0;
            BeltRunning = packet != null && !packet.BeltStopped && available && speed > 0;
            Status = status;
            Source = packet?.Source ?? "unknown";
            DeviceDistanceM = packet?.DistanceM;
            DeviceElapsedS = packet?.ElapsedS;
            Steps = packet?.Steps;
        }
    }

    /// <summary>Thread-safe latest snapshot with an independent, monotonic freshness clock.</summary>
    public sealed class TelemetryState
    {
        // This X21 reports about every two seconds. Allow one second of jitter.
        public const double FreshForSeconds = 3;
        private readonly object sync = new object();
        private TelemetryPacket packet;
        private double speedObservedAt;
        private double lastReceivedAt;
        private bool transportAvailable;
        private long safetyEpoch;
        private string transportStatus = "Waiting for walking input";

        public void Accept(TelemetryPacket incoming, double now)
        {
            if (incoming == null || double.IsNaN(now) || double.IsInfinity(now) || now < 0)
                throw new ArgumentException("A packet and monotonic timestamp are required.");
            lock (sync)
            {
                if (packet != null && now < lastReceivedAt)
                    throw new InvalidOperationException("The receipt clock moved backwards.");
                var newConnection = packet == null || packet.ConnectionId != incoming.ConnectionId || packet.Source != incoming.Source;
                if (!newConnection && incoming.Sequence < packet.Sequence)
                    throw new FormatException("Out-of-order telemetry.");
                // Preserve a failure across a reconnect that completes between rendered frames.
                if (newConnection || !IsFresh(now)) safetyEpoch++;
                var observedAt = incoming.SpeedAgeMs.HasValue
                    ? now - incoming.SpeedAgeMs.Value / 1000.0
                    : double.NegativeInfinity;
                if (newConnection || incoming.Sequence > packet.Sequence)
                    speedObservedAt = observedAt;
                else
                    // Heartbeats can age a measurement, but never make it younger.
                    speedObservedAt = Math.Min(speedObservedAt, observedAt);
                packet = incoming;
                lastReceivedAt = now;
                transportAvailable = incoming.Connected;
                transportStatus = incoming.Connected ? "Input connected" : "Input disconnected";
            }
        }

        public void Disconnect(string status)
        {
            lock (sync)
            {
                if (transportAvailable) safetyEpoch++;
                transportAvailable = false;
                transportStatus = status;
            }
        }

        public InputReading Read(double now)
        {
            lock (sync)
            {
                var available = IsFresh(now);
                var speed = available && !packet.BeltStopped ? packet.UsableSpeedMps.Value : 0;
                var status = !transportAvailable ? transportStatus : !available
                    ? "Speed data expired" : speed == 0 ? "Belt stopped" : "Input connected";
                return new InputReading(available, speed, packet?.ConnectionId ?? Guid.Empty,
                    safetyEpoch, status, packet);
            }
        }

        private bool IsFresh(double now)
        {
            return transportAvailable && packet != null && packet.Connected && !packet.Stale &&
                packet.SpeedMps.HasValue && packet.UsableSpeedMps.HasValue &&
                now >= lastReceivedAt && now - lastReceivedAt < FreshForSeconds &&
                now >= speedObservedAt && now - speedObservedAt < FreshForSeconds;
        }
    }
}
