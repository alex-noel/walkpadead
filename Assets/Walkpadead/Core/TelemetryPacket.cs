using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Walkpadead.Core
{
    /// <summary>The version-1 Rust bridge contract. Null measurements remain unknown.</summary>
    public sealed class TelemetryPacket
    {
        public Guid ConnectionId { get; private set; }
        public ulong Sequence { get; private set; }
        public bool Connected { get; private set; }
        public bool Stale { get; private set; }
        public string Source { get; private set; }
        public double? SpeedMps { get; private set; }
        public double? UsableSpeedMps { get; private set; }
        public ulong? SpeedAgeMs { get; private set; }
        public ulong? DistanceM { get; private set; }
        public ulong? ElapsedS { get; private set; }
        public ulong? Steps { get; private set; }
        public bool BeltStopped { get; private set; }

        public static TelemetryPacket Parse(string json)
        {
            var data = ReadObject(json);
            if (Integer(data, "schema_version") != 1)
                throw new FormatException("Unsupported telemetry schema.");
            if (!Guid.TryParse(Text(data, "connection_id"), out var id) || id == Guid.Empty)
                throw new FormatException("Invalid connection ID.");
            var source = Text(data, "input_source");
            if (source != "simulation" && source != "replay" && source != "live_device")
                throw new FormatException("Unknown input source.");
            var age = data["field_age_ms"] as JObject;
            if (age == null) throw new FormatException("Missing field ages.");
            var belt = Text(data, "belt_state", true);
            if (belt != null && belt != "running" && belt != "stopped" && belt != "unknown")
                throw new FormatException("Unknown belt state.");
            return new TelemetryPacket
            {
                ConnectionId = id,
                Sequence = Integer(data, "sequence"),
                Connected = Boolean(data, "connected"),
                Stale = Boolean(data, "stale"),
                Source = source,
                SpeedMps = Speed(data, "speed_mps"),
                UsableSpeedMps = Speed(data, "usable_speed_mps"),
                SpeedAgeMs = OptionalInteger(age, "speed_mps"),
                DistanceM = OptionalInteger(data, "distance_m"),
                ElapsedS = OptionalInteger(data, "elapsed_s"),
                Steps = OptionalInteger(data, "steps"),
                BeltStopped = belt == "stopped"
            };
        }

        internal static JObject ReadObject(string json)
        {
            if (json == null || json.Length > 32768) throw new FormatException("Message too large.");
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.MaxDepth = 12;
                var result = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) throw new FormatException("Trailing JSON content.");
                return result;
            }
        }

        internal static string Text(JObject data, string key, bool nullable = false)
        {
            var value = data[key];
            if (nullable && (value == null || value.Type == JTokenType.Null)) return null;
            if (value?.Type != JTokenType.String) throw new FormatException("Invalid " + key + ".");
            return (string)value;
        }

        internal static ulong Integer(JObject data, string key)
        {
            var value = data[key];
            if (value?.Type != JTokenType.Integer) throw new FormatException("Invalid " + key + ".");
            try { return value.Value<ulong>(); }
            catch (Exception e) when (e is OverflowException || e is FormatException || e is InvalidCastException)
            { throw new FormatException("Invalid " + key + ".", e); }
        }

        private static ulong? OptionalInteger(JObject data, string key)
        {
            return data[key] == null || data[key].Type == JTokenType.Null ? (ulong?)null : Integer(data, key);
        }

        private static bool Boolean(JObject data, string key)
        {
            if (data[key]?.Type != JTokenType.Boolean) throw new FormatException("Invalid " + key + ".");
            return (bool)data[key];
        }

        private static double? Speed(JObject data, string key)
        {
            var value = data[key];
            if (value == null) throw new FormatException("Missing " + key + ".");
            if (value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
                throw new FormatException("Invalid " + key + ".");
            var speed = (double)value;
            // Desktop gameplay currently supports belt telemetry from 0–20 km/h.
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0 || speed > 20.0 / 3.6)
                throw new FormatException("Speed outside supported range.");
            return speed;
        }
    }
}
