using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Mizon.API;

internal static class AccountPresenceChecks
{
    public static async Task RunAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        await using var server = new SocketChecks.TestHub();
        using var http = new HttpClient();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var api = new MizonApi(http, cache);
        var clientId = Guid.NewGuid();
        var bearer = "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{clientId:D}\"}}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";
        api.SetToken(bearer);
        await using var socket = new MizonSocket(api, server.Url);
        await socket.StartAsync(token);
        var first = await server.Sessions.Reader.ReadAsync(token);
        await socket.SetWindowPresenceAsync(false, token);
        Ensure((await first.Invocations.Reader.ReadAsync(token)).Target == "SetWindowPresence", "Local inactive focus is reported.");
        await first.SendAccountPresenceAsync(new()
        {
            ClientPublicId = clientId, IsOnline = true, Revision = 50,
        }, token);
        await socket.GetOnlineFollowerCountAsync(token);
        Ensure(socket.AccountPresence is null, "Account activity is ignored until the caller opens a watch.");

        first.HoldNextAccountPresence = true;
        var watching = socket.WatchAccountPresenceAsync(token);
        Ensure((await first.Invocations.Reader.ReadAsync(token)).Target == "WatchAccountPresence", "The watcher invokes the authenticated no-argument method.");
        await first.SendAccountPresenceAsync(new() { ClientPublicId = clientId, IsOnline = true, Revision = 2 }, token);
        await first.ReleaseAccountPresenceAsync(token);
        var accepted = await watching;
        Ensure(accepted is { IsOnline: true, Revision: 2 } && socket.WindowPresence is { IsOnline: false },
            "The newer event wins over the delayed initial snapshot without changing the local acknowledgment.");

        await first.SendAccountPresenceAsync(new() { ClientPublicId = Guid.NewGuid(), IsOnline = false, Revision = 10 }, token);
        await first.SendAccountPresenceAsync(new() { ClientPublicId = clientId, IsOnline = false, Revision = 1 }, token);
        await socket.GetOnlineFollowerCountAsync(token);
        Ensure(socket.AccountPresence is { IsOnline: true, Revision: 2 }, "Wrong-account and older revisions stay rejected.");
        var observedOffline = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        socket.AccountPresenceChanged += (_, state) =>
        {
            if (state is { IsOnline: false, Revision: 3 })
                observedOffline.TrySetResult();
        };
        await first.SendAccountPresenceAsync(new() { ClientPublicId = clientId, IsOnline = false, Revision = 3 }, token);
        await observedOffline.Task.WaitAsync(token);

        using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            first.HoldNextAccountPresence = true;
            var pending = socket.WatchAccountPresenceAsync(cancelled.Token);
            await first.Invocations.Reader.ReadAsync(token);
            cancelled.Cancel();
            try { await pending; throw new InvalidOperationException("Expected watch cancellation."); }
            catch (OperationCanceledException) { }
            await first.SendAccountPresenceAsync(new() { ClientPublicId = clientId, IsOnline = true, Revision = 4 }, token);
            await first.ReleaseAccountPresenceAsync(token);
            await socket.GetOnlineFollowerCountAsync(token);
            Ensure(socket.AccountPresence is null, "A canceled watch ignores later events and its late reply.");
        }
        await socket.WatchAccountPresenceAsync(token);
        await first.Invocations.Reader.ReadAsync(token);

        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        socket.AccountPresenceChanged += (_, state) =>
        {
            if (state is null)
                lost.TrySetResult();
        };
        server.HoldNextHandshake = true;
        first.Abort();
        await lost.Task.WaitAsync(token);
        Ensure(socket.AccountPresence is null, "Disconnect invalidates the last aggregate snapshot.");
        var second = await server.Sessions.Reader.ReadAsync(token);
        second.ReleaseHandshake.TrySetResult();
        await socket.StartAsync(token);
        await second.Invocations.Reader.ReadAsync(token);
        Ensure((await socket.WatchAccountPresenceAsync(token)).Revision == 1,
            "A new explicit watch after reconnect can accept a fresh revision sequence.");

        api.ClearToken();
        Ensure(socket.AccountPresence is null, "Sign-out immediately makes the previous account snapshot inaccessible.");
        api.SetToken(bearer);
        Ensure(socket.AccountPresence is null, "Reusing the same bearer after sign-out cannot revive a previous session's snapshot.");
        try { await socket.WatchAccountPresenceAsync(token); }
        catch (InvalidOperationException)
        {
            Ensure(socket.AccountPresence is null, "The new session cannot watch on an earlier authenticated connection.");
            Console.WriteLine("Account presence SDK passed: source generation, inactive observer, initial race, cancellation, account isolation, reconnect and sign-out.");
            return;
        }
        throw new InvalidOperationException("Expected the earlier connection to be rejected.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ClientAccountPresenceResponse))]
internal partial class AccountPresenceTestJsonContext : JsonSerializerContext;
