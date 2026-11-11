using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;

namespace Mizon.API;

public sealed partial class MizonSocket
{
    private readonly object _windowPresenceGate = new();
    private WindowPresenceIntent? _windowPresenceIntent;
    private ClientWindowPresenceResponse? _windowPresence;
    private long _windowPresenceRevision;
    private Guid? _windowPresenceTransportClientId;
    private Guid? _windowPresenceConnectedClientId;
    private string? _windowPresenceConnectionId;
    private Task? _windowPresenceRecovery;

    /// <summary>
    /// Raised for confirmed changes and invalidation. Read WindowPresence to distinguish a current
    /// server acknowledgement from the synthetic offline notification used during disconnection.
    /// Callbacks run on the transport thread; dispatch UI work to its owning thread.
    /// </summary>
    public event EventHandler<ClientWindowPresenceResponse>? WindowPresenceChanged;

    public ClientWindowPresenceResponse? WindowPresence
    {
        get
        {
            lock (_windowPresenceGate)
                return IsConnected && _windowPresence is { } state && state.ClientPublicId == CurrentClientPublicId
                    && state.ConnectionId == _hubConnection.ConnectionId ? state : null;
        }
    }

    /// <summary>
    /// Remembers the latest authenticated main-window focus without opening another connection.
    /// The existing socket owner starts/stops the connection. Reconnect restores only this latest intent.
    /// </summary>
    public async Task SetWindowPresenceAsync(bool isActive, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var clientId = CurrentClientPublicId;
        WindowPresenceIntent? intent;
        lock (_windowPresenceGate)
        {
            // Commit intent before the first await: a queued inactive update invalidates an older online ack.
            intent = clientId is Guid id && id != Guid.Empty
                ? new(id, isActive, ++_windowPresenceRevision) : null;
            _windowPresenceIntent = intent;
            _windowPresence = null;
        }
        NotifyWindowPresenceChanged();
        if (intent is null || !IsConnected)
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        try
        {
            await _operationLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                await PublishLatestWindowPresenceAsync(cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            CancelWindowPresenceIntent(intent);
            throw;
        }
    }

    private Task<string?> GetWindowPresenceTransportTokenAsync()
    {
        var token = _mizonApi.GetToken();
        lock (_windowPresenceGate)
            _windowPresenceTransportClientId = TryGetClientPublicId(token);
        return Task.FromResult(token);
    }

    private void BeginWindowPresenceConnection()
    {
        lock (_windowPresenceGate)
        {
            if (_windowPresenceConnectionId == _hubConnection.ConnectionId)
                return;
            _windowPresenceConnectionId = _hubConnection.ConnectionId;
            _windowPresenceConnectedClientId = _windowPresenceTransportClientId;
            _windowPresence = null;
            if (_windowPresenceIntent?.ClientPublicId != _windowPresenceConnectedClientId)
                _windowPresenceIntent = null;
        }
    }

    // The caller owns _operationLock. It is also used by music membership and lifecycle operations.
    private async Task PublishLatestWindowPresenceAsync(CancellationToken cancellationToken)
    {
        WindowPresenceIntent? intent;
        lock (_windowPresenceGate)
        {
            intent = _windowPresenceIntent;
            if (intent is null || !IsConnected || intent.ClientPublicId != CurrentClientPublicId
                || intent.ClientPublicId != _windowPresenceConnectedClientId
                || _windowPresence?.Revision == intent.Revision)
            {
                return;
            }
        }

        var response = await _hubConnection.InvokeAsync<ClientWindowPresenceResponse>(
            "SetWindowPresence", intent.IsActive, intent.Revision, cancellationToken).ConfigureAwait(false);
        ApplyWindowPresence(response);
    }

    private void ApplyWindowPresence(ClientWindowPresenceResponse response)
    {
        lock (_windowPresenceGate)
        {
            var intent = _windowPresenceIntent;
            if (Volatile.Read(ref _disposeRequested) != 0 || intent is null || !IsConnected
                || response.ClientPublicId != intent.ClientPublicId
                || response.ClientPublicId != CurrentClientPublicId
                || response.ClientPublicId != _windowPresenceConnectedClientId
                || response.ConnectionId != _windowPresenceConnectionId
                || response.ConnectionId != _hubConnection.ConnectionId
                || response.Revision != intent.Revision || response.IsOnline != intent.IsActive
                || _windowPresence?.Revision == response.Revision)
            {
                return;
            }
            _windowPresence = response;
        }
        NotifyWindowPresenceChanged();
    }

    private void ResetWindowPresence(bool clearDesired)
    {
        lock (_windowPresenceGate)
        {
            _windowPresence = null;
            _windowPresenceConnectionId = null;
            _windowPresenceConnectedClientId = null;
            if (clearDesired)
            {
                _windowPresenceIntent = null;
                ++_windowPresenceRevision;
            }
        }
        NotifyWindowPresenceChanged();
    }

    private void NotifyWindowPresenceChanged()
    {
        ClientWindowPresenceResponse response;
        lock (_windowPresenceGate)
        {
            response = WindowPresence ?? new()
            {
                ClientPublicId = CurrentClientPublicId ?? Guid.Empty,
                IsOnline = false,
                Revision = _windowPresenceRevision,
            };
        }
        WindowPresenceChanged?.Invoke(this, response);
    }

    private void CancelWindowPresenceIntent(WindowPresenceIntent canceled)
    {
        lock (_windowPresenceGate)
        {
            if (!ReferenceEquals(_windowPresenceIntent, canceled))
                return;
            _windowPresence = null;
            _windowPresenceIntent = new(canceled.ClientPublicId, false, ++_windowPresenceRevision);
            if (!_lifetimeToken.IsCancellationRequested && _windowPresenceRecovery is not { IsCompleted: false })
                _windowPresenceRecovery = RecoverCanceledWindowPresenceAsync();
        }
        NotifyWindowPresenceChanged();
    }

    private async Task RecoverCanceledWindowPresenceAsync()
    {
        try
        {
            await _operationLock.WaitAsync(_lifetimeToken).ConfigureAwait(false);
            try
            {
                while (IsConnected)
                {
                    WindowPresenceIntent? intent;
                    lock (_windowPresenceGate) intent = _windowPresenceIntent;
                    await PublishLatestWindowPresenceAsync(_lifetimeToken).ConfigureAwait(false);
                    lock (_windowPresenceGate)
                    {
                        if (ReferenceEquals(intent, _windowPresenceIntent))
                            break;
                    }
                }
            }
            finally { _operationLock.Release(); }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Trace.TraceWarning("Mizon window presence cancellation cleanup failed: {0}", exception.GetType().Name);
        }
    }

    private sealed record WindowPresenceIntent(Guid ClientPublicId, bool IsActive, long Revision);
}
