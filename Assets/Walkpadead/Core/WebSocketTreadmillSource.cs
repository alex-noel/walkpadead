using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Walkpadead.Core
{
    public interface ITreadmillSource : IDisposable
    {
        InputReading Read();
    }

    /// <summary>Desktop-only adapter. Unity never runs on the receiver thread.</summary>
    public sealed class WebSocketTreadmillSource : ITreadmillSource, ITreadmillSpeedControl
    {
        private readonly TelemetryState state = new TelemetryState();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly object socketLock = new object();
        private readonly Task worker;
        private ClientWebSocket socket;
        private bool disposed;
        private BridgeConnection currentConnection;
        private int speedRequestPending;
        private Guid controlLockedConnection;
        private bool ControlLocked => controlLockedConnection != Guid.Empty && Read().ConnectionId == controlLockedConnection;
        public bool CanAdjustSpeed { get { lock (socketLock) return !disposed && !ControlLocked && currentConnection?.SpeedControlEnabled == true && socket?.State == WebSocketState.Open; } }
        public string SpeedRangeLabel { get { lock (socketLock) return ControlLocked ? "Control locked · restart bridge" : currentConnection?.SpeedControlEnabled == true ? $"{currentConnection.MinSpeedKmh:0.0}–{currentConnection.MaxSpeedKmh:0.0} km/h" : "Bridge is read-only"; } }
        public Task Completion => worker;

        public async Task<SpeedControlResult> AdjustSpeedAsync(InputReading input, int deltaTenths)
        {
            if (Interlocked.CompareExchange(ref speedRequestPending, 1, 0) != 0)
                return new SpeedControlResult("rejected", "Speed request already pending");
            try
            {
                BridgeConnection connection; CancellationToken token;
                lock (socketLock)
                {
                    if (disposed || socket?.State != WebSocketState.Open || currentConnection == null)
                        return new SpeedControlResult("rejected", "Walking input is disconnected");
                    if (ControlLocked)
                        return new SpeedControlResult("rejected", "Speed control locked · restart bridge", controlLocked: true);
                    connection = currentConnection; token = cancellation.Token;
                }
                var latest = Read();
                if (!latest.Available || latest.ConnectionId != input.ConnectionId || latest.SafetyEpoch != input.SafetyEpoch)
                    return new SpeedControlResult("rejected", "Walking input changed");
                var result = await SpeedControlWire.SendAsync(connection, input, deltaTenths, token).ConfigureAwait(false);
                if (result.ControlLocked)
                    lock (socketLock) controlLockedConnection = input.ConnectionId;
                return result;
            }
            finally { Interlocked.Exchange(ref speedRequestPending, 0); }
        }

        public WebSocketTreadmillSource(string connectionFile)
        {
            if (string.IsNullOrWhiteSpace(connectionFile)) throw new ArgumentException("Connection path required.");
            worker = Task.Run(() => RunAsync(connectionFile, cancellation.Token));
        }

        public InputReading Read() => state.Read(clock.Elapsed.TotalSeconds);

        private async Task RunAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Re-read on every attempt: bridge restarts use a new port and token.
                        var connection = BridgeConnection.ReadFile(path);
                        using (var client = new ClientWebSocket())
                        {
                            client.Options.SetRequestHeader("Authorization", "Bearer " + connection.Token);
                            client.Options.KeepAliveInterval = TimeSpan.FromSeconds(1);
                            lock (socketLock)
                            {
                                if (disposed) return;
                                socket = client;
                                currentConnection = connection;
                            }
                            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                            {
                                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                                await client.ConnectAsync(connection.Url, timeout.Token).ConfigureAwait(false);
                            }
                            await ReceiveAsync(client, connection.InputSource, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                    catch (FileNotFoundException) { state.Disconnect("Start the WalkingPad bridge"); }
                    catch (DirectoryNotFoundException) { state.Disconnect("Start the WalkingPad bridge"); }
                    catch (Exception e) when (e is FormatException || e is Newtonsoft.Json.JsonException)
                    { state.Disconnect("Invalid walking input — reconnecting"); }
                    catch (Exception)
                    {
                        // Do not expose the connection document, bearer token, or response body.
                        state.Disconnect("Walking input unavailable — reconnecting");
                    }
                    finally
                    {
                        lock (socketLock) { socket = null; currentConnection = null; }
                    }
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally { state.Disconnect("Input disconnected"); }
        }

        private async Task ReceiveAsync(ClientWebSocket client, string expectedSource, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var utf8 = new UTF8Encoding(false, true);
            using (var message = new MemoryStream())
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var received = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                        .ConfigureAwait(false);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        state.Disconnect("Input disconnected");
                        return;
                    }
                    if (received.MessageType != WebSocketMessageType.Text || message.Length + received.Count > 32768)
                        throw new FormatException("Expected bounded JSON telemetry.");
                    message.Write(buffer, 0, received.Count);
                    if (!received.EndOfMessage) continue;
                    var packet = TelemetryPacket.Parse(utf8.GetString(message.GetBuffer(), 0, (int)message.Length));
                    if (packet.Source != expectedSource)
                        throw new FormatException("Telemetry source does not match the connection document.");
                    state.Accept(packet, clock.Elapsed.TotalSeconds);
                    message.SetLength(0);
                    if (!packet.Connected) return;
                }
            }
        }

        public void Dispose()
        {
            lock (socketLock)
            {
                if (disposed) return;
                disposed = true;
                cancellation.Cancel();
                socket?.Abort();
            }
            state.Disconnect("Input disconnected");
            // Do not block Unity's main thread waiting for an async receive to unwind.
            _ = worker.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
        }
    }
}
