using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Walkpadead.Core;

internal static class Program
{
    private static readonly Guid Id = Guid.NewGuid();
    private static int passed;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--live-connection-file")
            {
                await LiveHardwareCheck(args[1]);
                Console.WriteLine($"PASS: {passed} hardware check");
                return 0;
            }
            Check("Offline simulator is explicit, labelled and invalidates on disposal", () =>
            {
                var input = new SimulatedTreadmillSource();
                var reading = input.Read();
                Require(reading.Available && reading.Source == "simulation" && reading.BeltRunning);
                Near(reading.SpeedMps, 3.0 / 3.6);
                input.Dispose();
                Require(!input.Read().Available && input.Read().SafetyEpoch != reading.SafetyEpoch);
            });
            Check("Converts 3 km/h to metres; preserves unknown metrics", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                var reading = state.Read(0);
                Near(reading.SpeedMps, 3.0 / 3.6); Require(reading.Available && reading.Steps == null);
            });
            Check("Receipt silence and per-field age independently expire speed", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1, age: 2500), 5);
                Require(state.Read(5.49).Available && !state.Read(5.5).Available);
                state.Accept(Packet(2), 6); Require(!state.Read(9).Available);
            });
            Check("Duplicate sequence cannot refresh movement", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                state.Accept(Packet(1), 2.9); Require(!state.Read(3).Available);
            });
            Check("Stop is zero; missing speed and disconnect are unavailable", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1, stopped: true), 0);
                Require(state.Read(0).Available); Near(state.Read(0).SpeedMps, 0);
                state.Accept(Packet(2, speed: null), 0.1); Require(!state.Read(0.1).Available);
                state.Accept(Packet(3, connected: false), 0.2); Require(!state.Read(0.2).Available);
            });
            Check("Bad schemas, nonfinite/negative speed and malformed JSON are rejected", () =>
            {
                foreach (var pair in new[] { ("schema_version", (object)2), ("speed_mps", -1.0),
                    ("usable_speed_mps", "NaN"), ("sequence", -1), ("connected", "true"),
                    ("connection_id", "bad"), ("speed_mps", 99.0) })
                {
                    var json = Json(1); json[pair.Item1] = JToken.FromObject(pair.Item2);
                    Throws(() => TelemetryPacket.Parse(json.ToString()));
                }
                Throws(() => TelemetryPacket.Parse("{\"schema_version\":1,\"schema_version\":1}"));
                Throws(() => TelemetryPacket.Parse(Json(1) + " {}"));
            });
            Check("Decreasing sequence and nonmonotonic clock are rejected", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(2), 10);
                Throws(() => state.Accept(Packet(1), 11));
                Throws(() => state.Accept(Packet(3), 9));
            });
            Check("Known input sources share the exact authenticated loopback contract", () =>
            {
                var valid = ConnectionJson("ws://127.0.0.1:12345/telemetry");
                Require(BridgeConnection.Parse(valid).Url.Port == 12345);
                foreach (var url in new[] { "ws://example.com:123/telemetry", "ws://127.0.0.1.evil.test/telemetry",
                    "ws://localhost/telemetry", "wss://127.0.0.1/telemetry", "ws://127.0.0.1/other",
                    "ws://127.0.0.1/telemetry?q=1", "ws://user:pass@127.0.0.1/telemetry" })
                    Throws(() => BridgeConnection.Parse(ConnectionJson(url)));
                foreach (var source in new[] { "simulation", "live_device", "replay" })
                    Require(BridgeConnection.Parse(valid.Replace("simulation", source)).InputSource == source);
                Throws(() => BridgeConnection.Parse(valid.Replace("simulation", "unknown")));
            });
            Check("Stale input pauses time, distance, damage and requires manual resume", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                var run = new RunSession(); Require(run.Resume(state.Read(0)));
                run.Tick(state.Read(0.1), 0.1); var distance = run.DistanceM; var elapsed = run.ElapsedSeconds;
                run.Tick(state.Read(3), 0.1); Require(run.Phase == RunPhase.Paused);
                run.Damage(50); Near(run.Health, 100); Near(run.DistanceM, distance); Near(run.ElapsedSeconds, elapsed);
                state.Accept(Packet(2), 3.1); run.Tick(state.Read(3.1), 0.1);
                Require(run.Phase == RunPhase.Paused && run.Resume(state.Read(3.1)));
            });
            Check("Reconnect between rendered frames still requires confirmation", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                var run = new RunSession(); run.Resume(state.Read(0));
                state.Disconnect("lost"); state.Accept(Packet(2), 0.1);
                run.Tick(state.Read(0.1), 0.1); Require(run.Phase == RunPhase.Paused);
                run.Resume(state.Read(0.1)); state.Accept(Packet(1, id: Guid.NewGuid()), 0.2);
                run.Tick(state.Read(0.2), 0.1); Require(run.Phase == RunPhase.Paused);
            });
            Check("Fresh sample after an unnoticed silence also pauses", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                var run = new RunSession(); run.Resume(state.Read(0));
                state.Accept(Packet(2), 3); run.Tick(state.Read(3), 0.1);
                Require(run.Phase == RunPhase.Paused);
            });
            Check("Changing input source pauses gameplay and updates its visible label", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                var run = new RunSession(); Require(run.Resume(state.Read(0)));
                state.Accept(Packet(1, source: "live_device"), 0.1);
                run.Tick(state.Read(0.1), 0.1);
                Require(run.Phase == RunPhase.Paused && state.Read(0.1).SourceLabel == "Live WalkingPad");
                Require(run.Resume(state.Read(0.1)));
            });
            Check("X21 two-second cadence tolerates jitter but a missing report expires", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1, source: "live_device"), 0);
                var run = new RunSession(); Require(run.Resume(state.Read(0)));
                Require(state.Read(2.1).Available);
                state.Accept(Packet(2, source: "live_device"), 2.1);
                run.Tick(state.Read(2.1), 0.1); Require(run.Phase == RunPhase.Running);
                run.Tick(state.Read(5.11), 0.1); Require(run.Phase == RunPhase.Paused);
            });
            Check("Reported speed maps exactly to metres with no target-speed prediction", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1, speed: 1.0), 0);
                var run = new RunSession(); Require(run.Resume(state.Read(0)));
                Require(Math.Abs(run.Tick(state.Read(0.5), 0.5) - 0.5) < 1e-9);
                state.Accept(Packet(2, speed: 5.0 / 3.6), 0.5);
                Require(Math.Abs(run.Tick(state.Read(1), 0.5) - 5.0 / 7.2) < 1e-9);
                Require(Math.Abs(run.SpeedMps - 5.0 / 3.6) < 1e-9);
            });
            Check("Reported stop and frame stalls never move the player", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(1), 0);
                var run = new RunSession(); run.Resume(state.Read(0)); run.Tick(state.Read(0.1), 0.1);
                Require(run.SpeedMps > 0); state.Accept(Packet(2, stopped: true), 0.2);
                Near(run.Tick(state.Read(0.2), 0.1), 0); Near(run.SpeedMps, 0);
                run.Tick(state.Read(0.3), 5); Require(run.Phase == RunPhase.Paused);
            });
            Check("Simulation completes a timed run with metre-based distance", () =>
            {
                var state = new TelemetryState(); state.Accept(Packet(0), 0);
                var run = new RunSession(); run.Resume(state.Read(0));
                for (ulong i = 1; i <= 1201; i++)
                { state.Accept(Packet(i), i * 0.1); run.Tick(state.Read(i * 0.1), 0.1); }
                Require(run.Phase == RunPhase.Escaped); Near(run.ElapsedSeconds, 120);
                Require(run.DistanceM > 99 && run.DistanceM < 100.1 && run.HordeGapM > 18);
            });
            foreach (var source in new[] { "simulation", "live_device", "replay" }) await SocketChecks(source);
            if (args.Length == 2 && args[0] == "--rust-bridge") await RustBridgeCheck(args[1]);
            Console.WriteLine($"PASS: {passed} checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task SocketChecks(string inputSource)
    {
        var directory = Path.Combine(Path.GetTempPath(), "walkpadead-csharp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "connection.json");
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var token = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, ConnectionJson($"ws://127.0.0.1:{port}/telemetry", token, inputSource));
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (var source = new WebSocketTreadmillSource(path))
            using (var client = await listener.AcceptTcpClientAsync(timeout.Token))
            using (var stream = client.GetStream())
            {
                var request = new StringBuilder(); var b = new byte[1];
                while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                { Require(await stream.ReadAsync(b, timeout.Token) == 1); request.Append((char)b[0]); }
                Require(request.ToString().Contains("Authorization: Bearer " + token));
                var key = "";
                foreach (var line in request.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
                    if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = line.Substring(18).Trim();
                var digest = System.Security.Cryptography.SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                var upgrade = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + Convert.ToBase64String(digest) + "\r\n\r\n");
                await stream.WriteAsync(upgrade, timeout.Token);
                using (var server = WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan))
                {
                    var bytes = Encoding.UTF8.GetBytes(Json(1, source: inputSource).ToString(Formatting.None));
                    await server.SendAsync(new ArraySegment<byte>(bytes, 0, 17), WebSocketMessageType.Text, false, timeout.Token);
                    await server.SendAsync(new ArraySegment<byte>(bytes, 17, bytes.Length - 17), WebSocketMessageType.Text, true, timeout.Token);
                    await Until(() => source.Read().Available, timeout.Token);
                    Near(source.Read().SpeedMps, 3.0 / 3.6);
                    Require(source.Read().Source == inputSource);
                    await Until(() => !source.Read().Available, timeout.Token);
                    Require(source.Read().SpeedMps == 0);
                    passed++; Console.WriteLine($"PASS {inputSource}: fragmented WebSocket data; silence expires");
                    await server.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(Json(2, source: inputSource).ToString(Formatting.None))), WebSocketMessageType.Text, true, timeout.Token);
                    await Until(() => source.Read().Available, timeout.Token);
                    if (inputSource == "live_device")
                    {
                        await server.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(Json(3, source: "simulation").ToString(Formatting.None))), WebSocketMessageType.Text, true, timeout.Token);
                    }
                    else await server.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token);
                    await Until(() => !source.Read().Available, timeout.Token);
                    passed++; Console.WriteLine(inputSource == "live_device"
                        ? "PASS Live connection rejects mislabeled simulation packets"
                        : $"PASS {inputSource}: socket close invalidates speed");
                }
                source.Dispose(); await source.Completion;
            }
            listener.Stop();
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task RustBridgeCheck(string binary)
    {
        var directory = Path.Combine(Path.GetTempPath(), "walkpadead-rust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "connection.json");
        var info = new ProcessStartInfo(binary) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var arg in new[] { "bridge", "--simulate", "--speed-kmh", "3", "--seconds", "17", "--demo-faults", "--connection-file", path }) info.ArgumentList.Add(arg);
        using (var process = Process.Start(info))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25)))
        using (var source = new WebSocketTreadmillSource(path))
        {
            try
            {
                var sawFresh = false; var sawStale = false; var sawStopped = false; var sawResumed = false;
                while (!process.HasExited)
                {
                    var reading = source.Read();
                    if (reading.Available && reading.SpeedMps > 0)
                    { sawFresh = true; if (sawStopped) sawResumed = true; }
                    if (sawFresh && !reading.Available) sawStale = true;
                    if (reading.Available && reading.SpeedMps == 0) sawStopped = true;
                    await Task.Delay(30, timeout.Token);
                }
                await Until(() => !source.Read().Available, timeout.Token);
                Require(process.ExitCode == 0 && sawFresh && sawStale && sawStopped && sawResumed && !File.Exists(path));
                passed++; Console.WriteLine("PASS Real Rust bridge → production C# client: speed, stale, stop, resume, disconnect, credential cleanup");
            }
            finally
            {
                source.Dispose(); await source.Completion;
                if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); }
                Directory.Delete(directory, true);
            }
        }
    }

    // Opt-in physical-device acceptance check. The caller starts a live bridge
    // after arranging a device session; this client has no device-control functionality.
    private static async Task LiveHardwareCheck(string path)
    {
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120)))
        using (var source = new WebSocketTreadmillSource(path))
        {
            await Until(() => source.Read().Available, timeout.Token);
            var reading = source.Read();
            Require(reading.Source == "live_device");
            var run = new RunSession(); Require(run.Resume(reading));
            var movement = run.Tick(reading, 0.1);
            Require(reading.SpeedMps == 0 ? movement == 0 : movement > 0);
            Console.WriteLine($"Observed live X21: speed={reading.SpeedMps * 3.6:F1} km/h, steps={reading.Steps?.ToString() ?? "unknown"}, elapsed={reading.DeviceElapsedS?.ToString() ?? "unknown"}, distance={reading.DeviceDistanceM?.ToString() ?? "unknown"}");
            await Until(() => !File.Exists(path), timeout.Token);
            await Until(() => !source.Read().Available, timeout.Token);
            run.Tick(source.Read(), 0.1); Require(run.Phase == RunPhase.Paused);
            source.Dispose(); await source.Completion;
            passed++; Console.WriteLine("PASS Physical X21 → Rust BLE bridge → production C# client/gameplay core; shutdown pauses movement");
        }
    }

    private static async Task Until(Func<bool> predicate, CancellationToken cancellation)
    { while (!predicate()) await Task.Delay(20, cancellation); }
    private static void Check(string label, Action check) { check(); passed++; Console.WriteLine("PASS " + label); }
    private static void Require(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
    private static void Near(double actual, double expected) => Require(Math.Abs(actual - expected) < 0.00001);
    private static void Throws(Action action)
    { try { action(); } catch (Exception e) when (e is FormatException || e is JsonException || e is InvalidOperationException) { return; } throw new Exception("Expected rejection."); }

    private static TelemetryPacket Packet(ulong sequence, double? speed = 3.0 / 3.6, ulong age = 0, bool connected = true, bool stopped = false, Guid? id = null, string source = "simulation")
        => TelemetryPacket.Parse(Json(sequence, speed, age, connected, stopped, id, source).ToString());
    private static JObject Json(ulong sequence, double? speed = 3.0 / 3.6, ulong age = 0, bool connected = true, bool stopped = false, Guid? id = null, string source = "simulation")
        => JObject.FromObject(new { schema_version = 1, connection_id = id ?? Id, sequence, connected,
            stale = false, input_source = source, speed_mps = speed, usable_speed_mps = speed,
            field_age_ms = new { speed_mps = age }, belt_state = stopped ? "stopped" : "running",
            distance_m = (ulong?)12, elapsed_s = (ulong?)4, steps = (ulong?)null });
    private static string ConnectionJson(string url, string token = "12345678123456781234567812345678", string source = "simulation")
        => JsonConvert.SerializeObject(new { schema_version = 1, url, token, input_source = source });
}
