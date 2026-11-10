using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;
using Mizon.API;

internal static class SocketChecks
{
    public static async Task RunAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var token = timeout.Token;
        await using var server = new TestHub();
        using var httpClient = new HttpClient();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var api = new MizonApi(httpClient, cache);
        var clientId = Guid.NewGuid();
        api.SetToken(CreateToken(clientId));
        await using var socket = new MizonSocket(api, server.Url);
        Ensure(socket.CurrentClientPublicId == clientId, "JWT subject is decoded.");
        Ensure(socket.CurrentClientPublicId == clientId, "JWT subject cache is stable.");
        api.SetToken("e30.eyJzdWIiOjF9.signature");
        Ensure(socket.CurrentClientPublicId is null, "A non-string JWT subject is rejected safely.");
        api.SetToken(CreateToken(clientId));

        var snapshot = new TaskCompletionSource<MizonSocket.MusicListenersSnapshotEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playback = new TaskCompletionSource<MizonSocket.MusicListenerUpdatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removed = new TaskCompletionSource<MizonSocket.MusicListenerRemovedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewers = new TaskCompletionSource<MizonSocket.EntityViewersCountChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        socket.MusicListenersSnapshotReceived += (_, value) => snapshot.TrySetResult(value);
        socket.MusicListenerUpdated += (_, value) => playback.TrySetResult(value);
        socket.MusicListenerRemoved += (_, value) => removed.TrySetResult(value);
        socket.EntityViewersCountChanged += (_, value) => viewers.TrySetResult(value);

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => socket.StartAsync(token)));
        var firstSession = await server.Sessions.Reader.ReadAsync(token);
        Ensure(server.ConnectionCount == 1, "Concurrent starts establish one connection.");
        await socket.RegisterDeviceAsync(token);
        var firstRegistration = await firstSession.Invocations.Reader.ReadAsync(token);
        Ensure(firstRegistration.Target == "RegisterDevice", "Device registration reaches the hub.");

        var musicId = Guid.NewGuid();
        await socket.JoinEntityAsync("Music", musicId, token);
        var firstJoin = await firstSession.Invocations.Reader.ReadAsync(token);
        Ensure(firstJoin.Target == "JoinEntity" && firstJoin.EntityId == musicId, "Entity join reaches the hub.");
        var listeners = await snapshot.Task.WaitAsync(token);
        Ensure(listeners.MusicPublicId == musicId && listeners.Listeners.Count == 1
            && listeners.Listeners[0].ClientPublicId == TestHub.ListenerId
            && listeners.Listeners[0].FirstName == "آزمایش"
            && listeners.Listeners[0].UpdatedAtUtc.Year == 2026, "Generated metadata reads the presence snapshot and timestamps.");
        Ensure((await viewers.Task.WaitAsync(token)).Count == 2, "Primitive SignalR arguments deserialize.");

        await socket.JoinEntityAsync("music", musicId, token);
        await socket.PublishMusicPlaybackAsync(musicId, TimeSpan.FromSeconds(-1), true, token);
        var firstPlayback = await firstSession.Invocations.Reader.ReadAsync(token);
        Ensure(firstPlayback.Target == "UpdateMusicPlayback" && firstPlayback.PositionMilliseconds == 0,
            "Repeated joins are skipped; negative playback is clamped.");
        Ensure((await playback.Task.WaitAsync(token)).Listener.IsPlaying, "Generated metadata reads a single listener.");

        var otherMusicId = Guid.NewGuid();
        await Task.WhenAll(socket.JoinEntityAsync("Music", otherMusicId, token), socket.LeaveEntityAsync(token));
        Ensure((await firstSession.Invocations.Reader.ReadAsync(token)).Target == "JoinEntity", "Navigation joins in order.");
        Ensure((await firstSession.Invocations.Reader.ReadAsync(token)).Target == "LeaveEntity", "Navigation leaves in order.");
        Ensure((await removed.Task.WaitAsync(token)).ClientPublicId == TestHub.ListenerId, "Removal events deserialize.");
        await socket.PublishMusicPlaybackAsync(otherMusicId, TimeSpan.FromSeconds(12), false, token);
        await socket.JoinEntityAsync("Music", musicId, token);
        Ensure((await firstSession.Invocations.Reader.ReadAsync(token)).Target == "JoinEntity", "No playback is sent after leaving.");

        var refreshedClientId = Guid.NewGuid();
        var refreshedToken = CreateToken(refreshedClientId);
        api.SetToken(refreshedToken);
        Ensure(socket.CurrentClientPublicId == refreshedClientId, "Identity updates when the token changes.");
        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        socket.HubConnectionStateChanged += (_, state) =>
        {
            if (state == HubConnectionState.Connected && server.ConnectionCount > 1)
                reconnected.TrySetResult();
        };
        firstSession.Abort();
        var secondSession = await server.Sessions.Reader.ReadAsync(token);
        await reconnected.Task.WaitAsync(token);
        var restoredRegistration = await secondSession.Invocations.Reader.ReadAsync(token);
        var restoredJoin = await secondSession.Invocations.Reader.ReadAsync(token);
        Ensure(restoredRegistration.Target == "RegisterDevice" && restoredRegistration.Token == refreshedToken,
            "Reconnect registers with the current token.");
        Ensure(restoredJoin.Target == "JoinEntity" && restoredJoin.EntityId == musicId,
            "Reconnect restores the current membership before announcing readiness.");

        await socket.StopAsync(token);
        Ensure(!socket.IsConnected, "Stop closes the connection.");
        await socket.StartAsync(token);
        var thirdSession = await server.Sessions.Reader.ReadAsync(token);
        await socket.JoinEntityAsync("Music", otherMusicId, token);
        Ensure((await thirdSession.Invocations.Reader.ReadAsync(token)).Target == "JoinEntity",
            "Stop clears device and entity state before a later start.");

        thirdSession.HoldNextJoin = true;
        var pendingJoin = socket.JoinEntityAsync("Music", musicId, token);
        Ensure((await thirdSession.Invocations.Reader.ReadAsync(token)).Target == "JoinEntity", "An invocation is pending during disposal.");
        await Task.WhenAll(socket.DisposeAsync().AsTask(), socket.DisposeAsync().AsTask()).WaitAsync(token);
        try
        {
            await pendingJoin;
            throw new InvalidOperationException("Disposal did not cancel the pending invocation.");
        }
        catch (OperationCanceledException) { }
        Ensure(!socket.IsConnected, "Disposal closes the transport and is idempotent.");
        try
        {
            await socket.StartAsync(token);
            throw new InvalidOperationException("A disposed socket restarted.");
        }
        catch (ObjectDisposedException) { }
        Console.WriteLine("Socket checks passed: protocol metadata, concurrent lifecycle, navigation, reconnect, token refresh, and disposal.");
    }

    private static string CreateToken(Guid id) => "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{id:D}\"}}"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TestHub : IAsyncDisposable
    {
        public static readonly Guid ListenerId = Guid.Parse("b943eced-70bc-42bb-8ef0-94c4e1d603ff");
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentBag<Task> _requests = [];
        private readonly Task _acceptTask;
        private int _connectionCount;
        public int ConnectionCount => Volatile.Read(ref _connectionCount);
        public Channel<Session> Sessions { get; } = Channel.CreateUnbounded<Session>();
        public string Url { get; }

        public TestHub()
        {
            using var portReservation = new TcpListener(IPAddress.Loopback, 0);
            portReservation.Start();
            var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
            portReservation.Stop();
            var root = $"http://127.0.0.1:{port}/";
            Url = root + "hub";
            _listener.Prefixes.Add(root);
            _listener.Start();
            _acceptTask = AcceptAsync();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync().WaitAsync(_cancellation.Token);
                    _requests.Add(HandleAsync(context));
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
            catch (HttpListenerException) when (_cancellation.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) { }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            if (context.Request.Url!.AbsolutePath.EndsWith("/negotiate", StringComparison.Ordinal))
            {
                var id = Guid.NewGuid().ToString("N");
                var body = Encoding.UTF8.GetBytes($"{{\"negotiateVersion\":1,\"connectionId\":\"{id}\",\"connectionToken\":\"{id}\",\"availableTransports\":[{{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\",\"Binary\"]}}]}}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body, _cancellation.Token);
                context.Response.Close();
                return;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var webSocket = await context.AcceptWebSocketAsync(null);
            using var session = new Session(webSocket.WebSocket);
            Interlocked.Increment(ref _connectionCount);
            await Sessions.Writer.WriteAsync(session, _cancellation.Token);
            await session.RunAsync(_cancellation.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Close();
            await _acceptTask;
            try { await Task.WhenAll(_requests); }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
            _cancellation.Dispose();
        }

        public sealed record Invocation(string Target, Guid? EntityId = null, string? Token = null, long? PositionMilliseconds = null);

        public sealed class Session(WebSocket socket) : IDisposable
        {
            private readonly WebSocket _socket = socket;
            private Guid _entityId;
            public volatile bool HoldNextJoin;
            public Channel<Invocation> Invocations { get; } = Channel.CreateUnbounded<Invocation>();
            public void Abort() => _socket.Abort();

            public async Task RunAsync(CancellationToken cancellationToken)
            {
                try
                {
                    var pending = new StringBuilder();
                    var buffer = new byte[8192];
                    while (_socket.State == WebSocketState.Open)
                    {
                        var received = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                        if (received.MessageType == WebSocketMessageType.Close)
                        {
                            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                            return;
                        }

                        pending.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
                        while (pending.ToString().IndexOf('\u001e') is var separator && separator >= 0)
                        {
                            var message = pending.ToString(0, separator);
                            pending.Remove(0, separator + 1);
                            using var document = JsonDocument.Parse(message);
                            var root = document.RootElement;
                            if (root.TryGetProperty("protocol", out _))
                            {
                                await SendAsync("{}", cancellationToken);
                                continue;
                            }

                            if (!root.TryGetProperty("target", out var target))
                                continue;
                            var name = target.GetString()!;
                            var arguments = root.GetProperty("arguments");
                            var invocation = name switch
                            {
                                "JoinEntity" => new Invocation(name, arguments[1].GetGuid()),
                                "RegisterDevice" => new Invocation(name, Token: arguments[0].GetString()),
                                "UpdateMusicPlayback" => new Invocation(name, arguments[0].GetGuid(), PositionMilliseconds: arguments[1].GetInt64()),
                                _ => new Invocation(name)
                            };
                            await Invocations.Writer.WriteAsync(invocation, cancellationToken);
                            if (name == "JoinEntity")
                            {
                                _entityId = arguments[1].GetGuid();
                                if (HoldNextJoin)
                                {
                                    HoldNextJoin = false;
                                    continue;
                                }
                                await SendEventAsync("EntityViewersCountUpdated", $"\"Music\",\"{_entityId:D}\",2", cancellationToken);
                                await SendEventAsync("MusicListenersSnapshot", $"\"{_entityId:D}\",[{Presence(1234, true)}]", cancellationToken);
                            }
                            else if (name == "UpdateMusicPlayback")
                                await SendEventAsync("MusicListenerUpdated", $"\"{_entityId:D}\",{Presence(arguments[1].GetInt64(), arguments[2].GetBoolean())}", cancellationToken);
                            else if (name == "LeaveEntity")
                                await SendEventAsync("MusicListenerRemoved", $"\"{_entityId:D}\",\"{ListenerId:D}\"", cancellationToken);
                            else if (name == "UnregisterDevice")
                                await SendEventAsync("UnregistrationConfirmed", "\"test\"", cancellationToken);

                            if (root.TryGetProperty("invocationId", out var id))
                                await SendAsync($"{{\"type\":3,\"invocationId\":{id.GetRawText()}}}", cancellationToken);
                        }
                    }
                }
                catch (WebSocketException) { }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _socket.State == WebSocketState.Aborted) { }
            }

            private Task SendEventAsync(string target, string arguments, CancellationToken cancellationToken) =>
                SendAsync($"{{\"type\":1,\"target\":\"{target}\",\"arguments\":[{arguments}]}}", cancellationToken);

            private async Task SendAsync(string json, CancellationToken cancellationToken) =>
                await _socket.SendAsync(Encoding.UTF8.GetBytes(json + "\u001e").AsMemory(), WebSocketMessageType.Text, true, cancellationToken);

            private static string Presence(long position, bool isPlaying) =>
                $"{{\"clientPublicId\":\"{ListenerId:D}\",\"firstName\":\"آزمایش\",\"lastName\":\"Socket\",\"username\":\"listener\",\"profileMediaPublicId\":null,\"positionMilliseconds\":{position},\"isPlaying\":{(isPlaying ? "true" : "false")},\"updatedAtUtc\":\"2026-11-08T00:00:00+00:00\"}}";

            public void Dispose() => _socket.Dispose();
        }
    }
}
