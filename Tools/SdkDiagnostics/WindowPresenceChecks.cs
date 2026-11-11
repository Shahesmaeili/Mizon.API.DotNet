using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Mizon.API;

internal static class WindowPresenceChecks
{
    public static async Task RunAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var token = timeout.Token;
        await using var server = new SocketChecks.TestHub();
        using var http = new HttpClient();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var api = new MizonApi(http, cache);
        var clientId = Guid.NewGuid();
        api.SetToken(CreateToken(clientId));
        await using var socket = new MizonSocket(api, server.Url);
        var observed = new ConcurrentQueue<ClientWindowPresenceResponse?>();
        socket.WindowPresenceChanged += (_, _) => observed.Enqueue(socket.WindowPresence);

        await socket.SetWindowPresenceAsync(true, token);
        Ensure(!socket.IsConnected && socket.WindowPresence is null && server.ConnectionCount == 0,
            "Disconnected desired focus is remembered without starting another socket or claiming online.");
        await socket.StartAsync(token);
        var first = await server.Sessions.Reader.ReadAsync(token);
        var active = await first.Invocations.Reader.ReadAsync(token);
        Ensure(active is { Target: "SetWindowPresence", IsActive: true, Revision: > 0 }
            && socket.WindowPresence is { IsOnline: true, ClientPublicId: var current } && current == clientId,
            "Connection restore exchanges the typed presence DTO using generated SignalR JSON.");

        await socket.SetWindowPresenceAsync(false, token);
        await first.Invocations.Reader.ReadAsync(token);
        first.HoldNextPresence = true;
        var pendingActive = socket.SetWindowPresenceAsync(true, token);
        var delayed = await first.Invocations.Reader.ReadAsync(token);
        var onlineCount = observed.Count(value => value?.IsOnline == true);
        var pendingInactive = socket.SetWindowPresenceAsync(false, token);
        Ensure(socket.WindowPresence is null, "New intent invalidates confirmation synchronously before awaiting the busy gate.");
        await first.ReleasePresenceAsync(token);
        await Task.WhenAll(pendingActive, pendingInactive);
        var inactive = await first.Invocations.Reader.ReadAsync(token);
        Ensure(socket.WindowPresence is { IsOnline: false }
            && inactive.Revision > delayed.Revision
            && observed.Count(value => value?.IsOnline == true) == onlineCount,
            "A delayed online event AND completion cannot overtake the newer inactive intent.");

        var accepted = socket.WindowPresence!;
        await first.SendWindowPresenceAsync(new() { ClientPublicId = Guid.NewGuid(), ConnectionId = first.ConnectionId,
            IsOnline = true, Revision = accepted.Revision }, token);
        await first.SendWindowPresenceAsync(new() { ClientPublicId = clientId, ConnectionId = "old-connection",
            IsOnline = true, Revision = accepted.Revision }, token);
        await first.SendWindowPresenceAsync(new() { ClientPublicId = clientId, ConnectionId = first.ConnectionId,
            IsOnline = true, Revision = accepted.Revision - 1 }, token);
        await socket.SetWindowPresenceAsync(false, token); // Protocol round trip after all injected events.
        await first.Invocations.Reader.ReadAsync(token);
        Ensure(observed.Count(value => value?.IsOnline == true) == onlineCount, "Identity, connection and revision mismatches stay rejected.");

        await socket.SetWindowPresenceAsync(true, token);
        await first.Invocations.Reader.ReadAsync(token);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        socket.WindowPresenceChanged += (_, _) => { if (socket.WindowPresence is null) lost.TrySetResult(); };
        server.HoldNextHandshake = true;
        first.Abort();
        await lost.Task.WaitAsync(token);
        var second = await server.Sessions.Reader.ReadAsync(token);
        await socket.SetWindowPresenceAsync(false, token);
        Ensure(socket.WindowPresence is null, "Reconnect reports offline until a current connection confirms focus.");
        second.ReleaseHandshake.TrySetResult();
        var restored = await second.Invocations.Reader.ReadAsync(token);
        await UntilAsync(() => socket.WindowPresence is { IsOnline: false }, token);
        Ensure(restored is { Target: "SetWindowPresence", IsActive: false }
            && socket.WindowPresence!.ConnectionId == second.ConnectionId,
            "Reconnect publishes the latest focus changed while disconnected, not the former online state.");

        second.HoldNextPresence = true;
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(token);
        var canceledActive = socket.SetWindowPresenceAsync(true, canceled.Token);
        await second.Invocations.Reader.ReadAsync(token);
        onlineCount = observed.Count(value => value?.IsOnline == true);
        canceled.Cancel();
        try { await canceledActive; throw new InvalidOperationException("Expected cancellation."); }
        catch (OperationCanceledException) { }
        var cleanup = await second.Invocations.Reader.ReadAsync(token);
        Ensure(cleanup.IsActive == false, "Canceled current focus is withdrawn with a newer offline revision.");
        await second.ReleasePresenceAsync(token);
        await UntilAsync(() => socket.WindowPresence is { IsOnline: false }, token);
        Ensure(observed.Count(value => value?.IsOnline == true) == onlineCount, "Late completion after cancellation cannot turn presence online.");
        var beforeCanceledCall = socket.WindowPresence;
        using (var preCanceled = new CancellationTokenSource())
        {
            preCanceled.Cancel();
            try { await socket.SetWindowPresenceAsync(true, preCanceled.Token); throw new InvalidOperationException("Expected pre-cancellation."); }
            catch (OperationCanceledException) { }
            Ensure(ReferenceEquals(beforeCanceledCall, socket.WindowPresence), "Pre-cancellation does not mutate confirmed state or send a request.");
        }

        second.HoldNextPresence = true;
        var beforeStop = socket.SetWindowPresenceAsync(true, token);
        await second.Invocations.Reader.ReadAsync(token);
        var stopping = socket.StopAsync(token);
        var queuedDuringStop = socket.SetWindowPresenceAsync(true, token);
        await second.ReleasePresenceAsync(token);
        await Task.WhenAll(beforeStop, stopping, queuedDuringStop);
        Ensure(socket.WindowPresence is null, "Stop clears confirmation and desired focus immediately.");
        await socket.StartAsync(token);
        var third = await server.Sessions.Reader.ReadAsync(token);
        Ensure(socket.WindowPresence is null && !third.Invocations.Reader.TryRead(out _), "A fresh start does not restore focus queued while the previous login was stopping.");
        await socket.SetWindowPresenceAsync(true, token);
        await third.Invocations.Reader.ReadAsync(token);
        api.SetToken(CreateToken(Guid.NewGuid()));
        Ensure(socket.WindowPresence is null, "An account-token identity change cannot expose the old account's confirmation.");
        await socket.SetWindowPresenceAsync(true, token);
        Ensure(socket.WindowPresence is null && !third.Invocations.Reader.TryRead(out _), "A new account cannot publish over the old authenticated connection.");

        await socket.StopAsync(token);
        await socket.SetWindowPresenceAsync(true, token);
        await socket.StartAsync(token);
        var fourth = await server.Sessions.Reader.ReadAsync(token);
        await fourth.Invocations.Reader.ReadAsync(token);
        fourth.HoldNextPresence = true;
        var pendingDuringDispose = socket.SetWindowPresenceAsync(false, token);
        await fourth.Invocations.Reader.ReadAsync(token);
        await socket.DisposeAsync();
        try { await pendingDuringDispose; throw new InvalidOperationException("Expected disposal cancellation."); }
        catch (OperationCanceledException) { }
        Ensure(socket.WindowPresence is null, "Disposal cancels the in-flight presence invocation and invalidates confirmation.");
        Console.WriteLine("Window presence SDK passed: generated JSON, latest intent, stale/wrong identity rejection, reconnect, cancellation withdrawal, stop/sign-out and disposal.");
    }

    private static async Task UntilAsync(Func<bool> predicate, CancellationToken token)
    {
        while (!predicate()) await Task.Delay(10, token);
    }

    private static string CreateToken(Guid id) => "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{id:D}\"}}"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ClientWindowPresenceResponse))]
internal partial class WindowPresenceTestJsonContext : JsonSerializerContext;
