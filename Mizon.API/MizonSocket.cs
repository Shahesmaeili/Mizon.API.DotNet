using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Text;
using System.Text.Json;

namespace Mizon.API;

public sealed class MizonSocket : IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly MizonApi _mizonApi;
    private readonly SemaphoreSlim _startConnectionLock = new(1, 1);
    private string? _currentEntityType;
    private Guid? _currentEntityId;

    public event EventHandler<HubConnectionState>? HubConnectionStateChanged;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;
    public Guid? CurrentClientPublicId => TryGetClientPublicId(_mizonApi.GetToken());

    public MizonSocket(MizonApi mizonApi, string hubUrl)
    {
        _mizonApi = mizonApi;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Access token provider reads current token from MizonApi
                options.AccessTokenProvider = () => Task.FromResult(_mizonApi.GetToken());
                options.CloseTimeout = TimeSpan.FromSeconds(10);
            })
            .WithAutomaticReconnect()
            .Build();


        _hubConnection.Closed += OnClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;

        // Example server-to-client method name: "ReceiveNotification"
        _hubConnection.On<string, string?>("ReceiveNotification", (type, payload) =>
        {
            // NotificationReceived?.Invoke(type, payload);
        });

        // Server will send viewer updates for an entity: (entityType, entityId, count)
        _hubConnection.On<string, Guid, int>("EntityViewersCountUpdated", (entityType, entityId, count) =>
        {
            EntityViewersCountChanged?.Invoke(this, new EntityViewersCountChangedEventArgs(entityType, entityId, count));
        });

        _hubConnection.On<Guid, List<MusicListenerPresence>>("MusicListenersSnapshot", (musicPublicId, listeners) =>
        {
            MusicListenersSnapshotReceived?.Invoke(this, new MusicListenersSnapshotEventArgs(musicPublicId, listeners));
        });

        _hubConnection.On<Guid, MusicListenerPresence>("MusicListenerUpdated", (musicPublicId, listener) =>
        {
            MusicListenerUpdated?.Invoke(this, new MusicListenerUpdatedEventArgs(musicPublicId, listener));
        });

        _hubConnection.On<Guid, Guid>("MusicListenerRemoved", (musicPublicId, clientPublicId) =>
        {
            MusicListenerRemoved?.Invoke(this, new MusicListenerRemovedEventArgs(musicPublicId, clientPublicId));
        });
    }


    private Task OnClosed(Exception? exception)
    {
        Console.WriteLine($"Socket: {exception?.Message}");
        HubConnectionStateChanged?.Invoke(this, _hubConnection.State);
        return Task.CompletedTask;
    }

    public event EventHandler<EntityViewersCountChangedEventArgs>? EntityViewersCountChanged;
    public event EventHandler<MusicListenersSnapshotEventArgs>? MusicListenersSnapshotReceived;
    public event EventHandler<MusicListenerUpdatedEventArgs>? MusicListenerUpdated;
    public event EventHandler<MusicListenerRemovedEventArgs>? MusicListenerRemoved;

    public async Task JoinEntityAsync(string entityType, Guid entityPublicId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            await StartAsync(cancellationToken);

        _currentEntityType = entityType;
        _currentEntityId = entityPublicId;

        await _hubConnection.InvokeAsync("JoinEntity", entityType, entityPublicId, cancellationToken);
    }

    public async Task LeaveEntityAsync(CancellationToken cancellationToken = default)
    {
        _currentEntityType = null;
        _currentEntityId = null;

        if (IsConnected)
            await _hubConnection.InvokeAsync("LeaveEntity", cancellationToken);
    }

    public Task PublishMusicPlaybackAsync(Guid musicPublicId, TimeSpan position, bool isPlaying, CancellationToken cancellationToken = default)
    {
        if (!IsConnected
            || !string.Equals(_currentEntityType, "Music", StringComparison.OrdinalIgnoreCase)
            || _currentEntityId != musicPublicId)
        {
            return Task.CompletedTask;
        }

        return _hubConnection.InvokeAsync(
            "UpdateMusicPlayback",
            musicPublicId,
            Math.Max(0, (long)position.TotalMilliseconds),
            isPlaying,
            cancellationToken);
    }

    public class EntityViewersCountChangedEventArgs : EventArgs
    {
        public string EntityType { get; }
        public Guid EntityId { get; }
        public int Count { get; }

        public EntityViewersCountChangedEventArgs(string entityType, Guid entityId, int count)
        {
            EntityType = entityType;
            EntityId = entityId;
            Count = count;
        }
    }

    public sealed class MusicListenersSnapshotEventArgs(Guid musicPublicId, IReadOnlyList<MusicListenerPresence> listeners) : EventArgs
    {
        public Guid MusicPublicId { get; } = musicPublicId;
        public IReadOnlyList<MusicListenerPresence> Listeners { get; } = listeners;
    }

    public sealed class MusicListenerUpdatedEventArgs(Guid musicPublicId, MusicListenerPresence listener) : EventArgs
    {
        public Guid MusicPublicId { get; } = musicPublicId;
        public MusicListenerPresence Listener { get; } = listener;
    }

    public sealed class MusicListenerRemovedEventArgs(Guid musicPublicId, Guid clientPublicId) : EventArgs
    {
        public Guid MusicPublicId { get; } = musicPublicId;
        public Guid ClientPublicId { get; } = clientPublicId;
    }

    private Task OnReconnecting(Exception? exception)
    {
        Console.WriteLine($"Socket: {exception?.Message}");

        HubConnectionStateChanged?.Invoke(this, _hubConnection.State);
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        Console.WriteLine($"Socket: {connectionId}");

        // re-join last known entity (best-effort)
        if (!string.IsNullOrEmpty(_currentEntityType) && _currentEntityId is Guid id)
        {
            _ = _hubConnection.InvokeAsync("JoinEntity", _currentEntityType, id);
        }

        HubConnectionStateChanged?.Invoke(this, _hubConnection.State);
        return Task.CompletedTask;
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
            return;

        await _startConnectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected || _hubConnection.State != HubConnectionState.Disconnected)
                return;

            await _hubConnection.StartAsync(cancellationToken);
            HubConnectionStateChanged?.Invoke(this, _hubConnection.State);
        }
        finally
        {
            _startConnectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
            return;

        await _hubConnection.StopAsync(cancellationToken);
        HubConnectionStateChanged?.Invoke(this, _hubConnection.State);
    }

    /// <summary>
    /// Example registration method. Adjust server method name and payload to match backend hub.
    /// </summary>
    public async Task RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        // Send device info derived from MizonApi (server method name: "RegisterDevice")
        await _hubConnection.InvokeAsync("RegisterDevice",
            _mizonApi.GetToken(), // optionally pass token or backend can read from access token
            cancellationToken);
    }

    public async Task UnregisterDeviceAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
            return;

        await _hubConnection.InvokeAsync("UnregisterDevice", cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        //try
        //{
        //    await StopAsync().ConfigureAwait(false);
        //    await _connection.DisposeAsync().ConfigureAwait(false);
        //}
        //catch
        //{
        //    // swallow — disposing best-effort
        //}
    }

    private static Guid? TryGetClientPublicId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenParts = token.Split('.');
        if (tokenParts.Length < 2)
            return null;

        try
        {
            var payload = tokenParts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("sub", out var subject)
                && Guid.TryParse(subject.GetString(), out var clientPublicId)
                ? clientPublicId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

public sealed class MusicListenerPresence
{
    public Guid ClientPublicId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public Guid? ProfileMediaPublicId { get; set; }
    public long PositionMilliseconds { get; set; }
    public bool IsPlaying { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
