using System;

namespace Walkpadead.Core
{
    /// <summary>Explicit offline demo input. Never substituted for a failed live connection.</summary>
    public sealed class SimulatedTreadmillSource : ITreadmillSource
    {
        private readonly Guid connectionId = Guid.NewGuid();
        private readonly InputReading connectedReading, disconnectedReading;
        private bool disposed;
        public SimulatedTreadmillSource(double speedKmh = 3)
        {
            if (double.IsNaN(speedKmh) || double.IsInfinity(speedKmh) || speedKmh < 0 || speedKmh > 5)
                throw new ArgumentOutOfRangeException(nameof(speedKmh));
            var speedMps = speedKmh / 3.6;
            var packet = TelemetryPacket.Parse("{\"schema_version\":1,\"connection_id\":\"" + connectionId +
                "\",\"sequence\":1,\"connected\":true,\"stale\":false,\"input_source\":\"simulation\"," +
                "\"speed_mps\":" + speedMps.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"usable_speed_mps\":" + speedMps.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"field_age_ms\":{\"speed_mps\":0},\"belt_state\":\"" + (speedMps > 0 ? "running" : "stopped") + "\"}");
            connectedReading = new InputReading(true, speedMps, connectionId, 1,
                "Offline simulator · " + speedKmh.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km/h", packet);
            disconnectedReading = new InputReading(false, 0, connectionId, 2, "Simulator disconnected", packet);
        }
        public InputReading Read() => disposed ? disconnectedReading : connectedReading;
        public void Dispose() => disposed = true;
    }
}
