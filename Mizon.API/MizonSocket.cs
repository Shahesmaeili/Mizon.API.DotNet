using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mizon.API;

public sealed class MizonSocket : IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly MizonApi _mizonApi;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly object _disposeLock = new();
    private readonly IDisposable[] _subscriptions;
    private TaskCompletionSource _connectionChanged = NewConnectionSignal();
    private volatile EntityMembership? _currentEntity;
    private volatile TokenIdentity? _tokenIdentity;
    private string? _joinedConnectionId;
    private bool _deviceRegistered;
    private int _stopVersion;
    private int _disposeRequested;
    private Task? _disposeTask;

    public event EventHandler<HubConnectionState>? HubConnectionStateChanged;
    public event EventHandler<EntityViewersCountChangedEventArgs>? EntityViewersCountChanged;
    public event EventHandler<MusicListenersSnapshotEventArgs>? MusicListenersSnapshotReceived;
    public event EventHandler<MusicListenerUpdatedEventArgs>? MusicListenerUpdated;
    public event EventHandler<MusicListenerRemovedEventArgs>? MusicListenerRemoved;

    public bool IsConnected => Volatile.Read(ref _disposeRequested) == 0 && _hubConnection.State == HubConnectionState.Connected;

    public Guid? CurrentClientPublicId
    {
        get
        {
            var token = _mizonApi.GetToken();
            var identity = _tokenIdentity;
            if (identity is null || !string.Equals(identity.Token, token, StringComparison.Ordinal))
            {
                identity = new(token, TryGetClientPublicId(token));
                _tokenIdentity = identity;
            }

            return identity.ClientPublicId;
        }
    }

    public MizonSocket(MizonApi mizonApi, string hubUrl)
    {
        ArgumentNullException.ThrowIfNull(mizonApi);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubUrl);
        _mizonApi = mizonApi;
        _lifetimeToken = _lifetimeCancellation.Token;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_mizonApi.GetToken());
                options.CloseTimeout = TimeSpan.FromSeconds(10);
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.TypeInfoResolver = MizonSocketJsonContext.Default)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.Closed += OnClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;

        _subscriptions =
        [
            _hubConnection.On<string, string?>("ReceiveNotification", static (_, _) => { }),
            _hubConnection.On<string>("UnregistrationConfirmed", static _ => { }),
            _hubConnection.On<string, Guid, int>("EntityViewersCountUpdated", (entityType, entityId, count) =>
                EntityViewersCountChanged?.Invoke(this, new(entityType, entityId, count))),
            _hubConnection.On<Guid, List<MusicListenerPresence>>("MusicListenersSnapshot", (musicPublicId, listeners) =>
                MusicListenersSnapshotReceived?.Invoke(this, new(musicPublicId, listeners))),
            _hubConnection.On<Guid, MusicListenerPresence>("MusicListenerUpdated", (musicPublicId, listener) =>
                MusicListenerUpdated?.Invoke(this, new(musicPublicId, listener))),
            _hubConnection.On<Guid, Guid>("MusicListenerRemoved", (musicPublicId, clientPublicId) =>
                MusicListenerRemoved?.Invoke(this, new(musicPublicId, clientPublicId)))
        ];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        var token = cancellation.Token;
        var stopVersion = Volatile.Read(ref _stopVersion);
        while (true)
        {
            Task connectionChanged;
            var started = false;
            await _operationLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (stopVersion != Volatile.Read(ref _stopVersion))
                    throw new OperationCanceledException("The socket was stopped while starting.", token);
                // Capture the signal before reading State so a reconnect cannot be missed.
                connectionChanged = Volatile.Read(ref _connectionChanged).Task;
                if (IsConnected)
                    return;

                if (_hubConnection.State == HubConnectionState.Disconnected)
                {
                    await _hubConnection.StartAsync(token).ConfigureAwait(false);
                    await RestoreConnectionStateAsync(token).ConfigureAwait(false);
                    started = true;
                }
            }
            finally
            {
                _operationLock.Release();
            }

            if (started)
            {
                NotifyConnectionState();
                return;
            }

            // Automatic reconnect owns the connection until it succeeds or closes.
            await connectionChanged.WaitAsync(token).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _stopVersion);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try
        {
            _currentEntity = null;
            _joinedConnectionId = null;
            _deviceRegistered = false;
            _tokenIdentity = null;
            await _hubConnection.StopAsync(cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
            SignalConnectionChanged();
        }

        NotifyConnectionState();
    }

    public async Task JoinEntityAsync(string entityType, Guid entityPublicId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        if (entityPublicId == Guid.Empty)
            throw new ArgumentException("The entity public ID cannot be empty.", nameof(entityPublicId));

        await StartAsync(cancellationToken).ConfigureAwait(false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try
        {
            if (_currentEntity is { } current
                && string.Equals(current.EntityType, entityType, StringComparison.OrdinalIgnoreCase)
                && current.EntityPublicId == entityPublicId
                && _joinedConnectionId == _hubConnection.ConnectionId
                && IsConnected)
            {
                return;
            }

            _currentEntity = new(entityType, entityPublicId);
            _joinedConnectionId = null;
            try
            {
                await _hubConnection.InvokeAsync("JoinEntity", entityType, entityPublicId, cancellation.Token).ConfigureAwait(false);
            }
            catch when (cancellation.IsCancellationRequested || IsConnected)
            {
                // Keep the intended destination across a dropped transport, but not a
                // canceled navigation or an explicit rejection by the connected hub.
                _currentEntity = null;
                throw;
            }
            _joinedConnectionId = _hubConnection.ConnectionId;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task LeaveEntityAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try
        {
            _currentEntity = null;
            _joinedConnectionId = null;
            if (IsConnected)
                await _hubConnection.InvokeAsync("LeaveEntity", cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task PublishMusicPlaybackAsync(Guid musicPublicId, TimeSpan position, bool isPlaying, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var entity = _currentEntity;
        if (!IsConnected || entity is null
            || !string.Equals(entity.EntityType, "Music", StringComparison.OrdinalIgnoreCase)
            || entity.EntityPublicId != musicPublicId)
        {
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try
        {
            // A pending update must never overtake navigation or a reconnect's JoinEntity.
            if (!IsConnected || !ReferenceEquals(entity, _currentEntity)
                || _joinedConnectionId != _hubConnection.ConnectionId)
            {
                return;
            }

            await _hubConnection.InvokeAsync("UpdateMusicPlayback", musicPublicId,
                Math.Max(0, position.Ticks / TimeSpan.TicksPerMillisecond), isPlaying, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try
        {
            await _hubConnection.InvokeAsync("RegisterDevice", _mizonApi.GetToken(), cancellation.Token).ConfigureAwait(false);
            _deviceRegistered = true;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task UnregisterDeviceAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try
        {
            _deviceRegistered = false;
            if (IsConnected)
                await _hubConnection.InvokeAsync("UnregisterDevice", cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private Task OnClosed(Exception? exception)
    {
        if (exception is not null)
            Trace.TraceWarning("Mizon socket closed: {0}", exception.Message);
        NotifyConnectionState();
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        if (exception is not null)
            Trace.TraceWarning("Mizon socket reconnecting: {0}", exception.Message);
        NotifyConnectionState();
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        try
        {
            await _operationLock.WaitAsync(_lifetimeToken).ConfigureAwait(false);
            try
            {
                if (IsConnected)
                    await RestoreConnectionStateAsync(_lifetimeToken).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Trace.TraceError("Mizon socket state restoration failed: {0}", exception.Message);
        }
        finally
        {
            NotifyConnectionState();
        }
    }

    private async Task RestoreConnectionStateAsync(CancellationToken cancellationToken)
    {
        if (_deviceRegistered)
            await _hubConnection.InvokeAsync("RegisterDevice", _mizonApi.GetToken(), cancellationToken).ConfigureAwait(false);

        if (_currentEntity is { } entity && _joinedConnectionId != _hubConnection.ConnectionId)
        {
            _joinedConnectionId = null;
            await _hubConnection.InvokeAsync("JoinEntity", entity.EntityType, entity.EntityPublicId, cancellationToken).ConfigureAwait(false);
            _joinedConnectionId = _hubConnection.ConnectionId;
        }
    }

    private void NotifyConnectionState()
    {
        SignalConnectionChanged();
        if (Volatile.Read(ref _disposeRequested) == 0)
            HubConnectionStateChanged?.Invoke(this, _hubConnection.State);
    }

    private void SignalConnectionChanged() => Interlocked.Exchange(ref _connectionChanged, NewConnectionSignal()).TrySetResult();
    private static TaskCompletionSource NewConnectionSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
            return new(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref _disposeRequested, 1);
        await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
        SignalConnectionChanged();
        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _hubConnection.Closed -= OnClosed;
            _hubConnection.Reconnecting -= OnReconnecting;
            _hubConnection.Reconnected -= OnReconnected;
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _currentEntity = null;
            _tokenIdentity = null;
            _joinedConnectionId = null;
            _deviceRegistered = false;
            HubConnectionStateChanged = null;
            EntityViewersCountChanged = null;
            MusicListenersSnapshotReceived = null;
            MusicListenerUpdated = null;
            MusicListenerRemoved = null;
            await _hubConnection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifetimeCancellation.Dispose();
            // No WaitHandle is allocated. Keeping the gate avoids racing canceled waiters.
            _operationLock.Release();
        }
    }

    private static Guid? TryGetClientPublicId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var start = token.IndexOf('.');
        var end = start >= 0 ? token.IndexOf('.', start + 1) : -1;
        if (start < 0 || end <= start + 1)
            return null;

        try
        {
            var payload = token[(start + 1)..end].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("sub", out var subject)
                && subject.ValueKind == JsonValueKind.String
                && Guid.TryParse(subject.GetString(), out var clientPublicId)
                ? clientPublicId : null;
        }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
    }

    private sealed record EntityMembership(string EntityType, Guid EntityPublicId);
    private sealed record TokenIdentity(string? Token, Guid? ClientPublicId);

    public class EntityViewersCountChangedEventArgs(string entityType, Guid entityId, int count) : EventArgs
    {
        public string EntityType { get; } = entityType;
        public Guid EntityId { get; } = entityId;
        public int Count { get; } = count;
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

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(MusicListenerPresence))]
[JsonSerializable(typeof(List<MusicListenerPresence>))]
internal partial class MizonSocketJsonContext : JsonSerializerContext;
