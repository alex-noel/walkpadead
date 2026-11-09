using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Walkpadead.Core
{
    public interface ITreadmillSpeedControl
    {
        bool CanAdjustSpeed { get; }
        string SpeedRangeLabel { get; }
        Task<SpeedControlResult> AdjustSpeedAsync(InputReading input, int deltaTenths);
    }

    public sealed class SpeedControlResult
    {
        public string Status { get; }
        public string Message { get; }
        public double? TargetKmh { get; }
        public bool ControlLocked { get; }
        public SpeedControlResult(string status, string message, double? targetKmh = null, bool controlLocked = false)
        { Status = status; Message = message; TargetKmh = targetKmh; ControlLocked = controlLocked || status == "unconfirmed"; }
    }

    internal static class SpeedControlWire
    {
        internal static async Task<SpeedControlResult> SendAsync(BridgeConnection connection, InputReading input,
            int deltaTenths, CancellationToken cancellation)
        {
            if (!connection.SpeedControlEnabled || !input.Available || !input.BeltRunning ||
                input.Source != connection.InputSource || (deltaTenths != -1 && deltaTenths != 1))
                return new SpeedControlResult("rejected", "Fresh running input and speed control are required");
            var baseTenths = (int)Math.Round(input.SpeedMps * 36.0);
            var target = (baseTenths + deltaTenths) / 10.0;
            if (target < connection.MinSpeedKmh || target > connection.MaxSpeedKmh)
                return new SpeedControlResult("rejected", "Speed limit reached");
            var id = Guid.NewGuid().ToString();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var socket = new ClientWebSocket())
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(14));
                socket.Options.SetRequestHeader("Authorization", "Bearer " + connection.Token);
                var endpoint = new UriBuilder(connection.Url) { Path = "/control" }.Uri;
                try
                {
                    await socket.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false);
                    var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                        request_id = id, connection_id = input.ConnectionId.ToString(), sequence = input.Sequence,
                        expected_speed_tenths = baseTenths, delta_tenths = deltaTenths
                    }));
                    await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                    var buffer = new byte[1024];
                    using (var message = new MemoryStream())
                    {
                        WebSocketReceiveResult received;
                        do
                        {
                            received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).ConfigureAwait(false);
                            if (received.MessageType != WebSocketMessageType.Text || message.Length + received.Count > 4096)
                                throw new FormatException("Invalid control response");
                            message.Write(buffer, 0, received.Count);
                        } while (!received.EndOfMessage);
                        var reply = JObject.Parse(new UTF8Encoding(false, true).GetString(message.ToArray()));
                        var status = (string)reply["status"];
                        if ((string)reply["request_id"] != id || (status != "confirmed" && status != "rejected" && status != "unconfirmed"))
                            throw new FormatException("Invalid control response");
                        var reported = reply["target_kmh"]?.Type == JTokenType.Null ? null : (double?)reply["target_kmh"];
                        if (status == "confirmed" && (!reported.HasValue || Math.Abs(reported.Value - target) > 0.001))
                            throw new FormatException("Unexpected confirmed target");
                        return new SpeedControlResult(status, (string)reply["message"] ?? status, reported,
                            (bool?)reply["control_locked"] ?? false);
                    }
                }
                catch (Exception)
                {
                    // Delivery may already have happened. Never resend an uncertain write.
                    return new SpeedControlResult("unconfirmed", "Speed request unconfirmed; bonuses disabled. No retry sent");
                }
            }
        }
    }
}
