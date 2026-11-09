using System;
using System.IO;
using System.Text;

namespace Walkpadead.Core
{
    public sealed class BridgeConnection
    {
        public Uri Url { get; private set; }
        public string Token { get; private set; }
        public string InputSource { get; private set; }
        public bool SpeedControlEnabled { get; private set; }
        public double MinSpeedKmh { get; private set; }
        public double MaxSpeedKmh { get; private set; }

        public static BridgeConnection ReadFile(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length > 4096) throw new FormatException("Connection file too large.");
                using (var reader = new StreamReader(file, new UTF8Encoding(false, true)))
                    return Parse(reader.ReadToEnd());
            }
        }

        public static BridgeConnection Parse(string json)
        {
            var data = TelemetryPacket.ReadObject(json);
            if (TelemetryPacket.Integer(data, "schema_version") != 1)
                throw new FormatException("Expected a version-1 walking input bridge.");
            var source = TelemetryPacket.Text(data, "input_source");
            if (source != "simulation" && source != "live_device" && source != "replay")
                throw new FormatException("Unknown bridge input source.");
            if (!Uri.TryCreate(TelemetryPacket.Text(data, "url"), UriKind.Absolute, out var url) ||
                url.Scheme != "ws" || url.Host != "127.0.0.1" || url.Port < 1 ||
                url.AbsolutePath != "/telemetry" || url.UserInfo.Length != 0 ||
                url.Query.Length != 0 || url.Fragment.Length != 0)
                throw new FormatException("Expected a loopback telemetry endpoint.");
            var token = TelemetryPacket.Text(data, "token");
            if (token.Length != 32 || !Guid.TryParseExact(token, "N", out _))
                throw new FormatException("Invalid bridge token.");
            var result = new BridgeConnection { Url = url, Token = token, InputSource = source };
            if (data["speed_control"] is Newtonsoft.Json.Linq.JObject limits)
            {
                var min = TelemetryPacket.Integer(limits, "min_tenths");
                var max = TelemetryPacket.Integer(limits, "max_tenths");
                if (min < 5 || max > 60 || min >= max || source == "replay")
                    throw new FormatException("Invalid speed-control limits.");
                result.SpeedControlEnabled = true;
                result.MinSpeedKmh = min / 10.0; result.MaxSpeedKmh = max / 10.0;
            }
            return result;
        }
    }
}
