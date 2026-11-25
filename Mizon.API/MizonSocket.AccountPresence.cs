using Microsoft.AspNetCore.SignalR.Client;

namespace Mizon.API;

public sealed partial class MizonSocket
{
    // Use the window presence gate. Account watches are tied to its authenticated transport identity.
    private long _accountPresenceTransportGeneration;
    private long _accountPresenceConnectionGeneration;
    private AccountPresenceWatch? _accountPresenceWatch;
    private ClientAccountPresenceResponse? _accountPresence;

    /// <summary>
    /// Raised on the transport thread when account presence changes or is invalidated.
    /// Read <see cref="AccountPresence"/> on the consuming thread for the latest state.
    /// </summary>
    public event EventHandler<ClientAccountPresenceResponse?>? AccountPresenceChanged;

    public ClientAccountPresenceResponse? AccountPresence
    {
        get
        {
            lock (_windowPresenceGate)
                return _accountPresenceWatch is { } watch && IsCurrentAccountWatch(watch) ? _accountPresence : null;
        }
    }

    /// <summary>
    /// Reads the authenticated account's snapshot and subscribes on this connection.
    /// Start the socket first and renew the watch after reconnect. Older servers can reject this method.
    /// </summary>
    public async Task<ClientAccountPresenceResponse> WatchAccountPresenceAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        AccountPresenceWatch watch;
        lock (_windowPresenceGate)
        {
            var generation = _mizonApi.ReadAuthentication().Generation;
            if (!IsConnected || _windowPresenceConnectionId is not { } connectionId
                || connectionId != _hubConnection.ConnectionId
                || _windowPresenceConnectedClientId is not Guid clientId || clientId == Guid.Empty
                || generation != _accountPresenceConnectionGeneration)
            {
                throw new InvalidOperationException("An authenticated socket connection is required to watch account presence.");
            }

            watch = new(clientId, connectionId, generation);
            if (_accountPresenceWatch is not { } previous || !IsCurrentAccountWatch(previous))
                _accountPresence = null;
            _accountPresenceWatch = watch;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        try
        {
            var response = await _hubConnection.InvokeAsync<ClientAccountPresenceResponse>(
                "WatchAccountPresence", cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            if (response is null || response.ClientPublicId != watch.ClientPublicId || response.Revision < 0)
                throw new InvalidDataException("The server returned an invalid account presence snapshot.");

            return ApplyAccountPresence(response, watch)
                ?? throw new OperationCanceledException("The authenticated presence connection changed.", cancellation.Token);
        }
        catch
        {
            ResetAccountPresence(watch);
            throw;
        }
    }

    private void ApplyAccountPresenceEvent(ClientAccountPresenceResponse response)
    {
        if (response is not null && Volatile.Read(ref _accountPresenceWatch) is { } watch)
            ApplyAccountPresence(response, watch);
    }

    private ClientAccountPresenceResponse? ApplyAccountPresence(ClientAccountPresenceResponse response, AccountPresenceWatch expected)
    {
        lock (_windowPresenceGate)
        {
            if (_accountPresenceWatch is not { } watch || !IsCurrentAccountWatch(watch)
                || !ReferenceEquals(watch, expected)
                || response.ClientPublicId != watch.ClientPublicId || response.Revision < 0)
            {
                return null;
            }
            if (_accountPresence is { } latest && latest.Revision >= response.Revision)
                return latest;

            _accountPresence = response;
        }
        AccountPresenceChanged?.Invoke(this, AccountPresence);
        return response;
    }

    private bool IsCurrentAccountWatch(AccountPresenceWatch watch) =>
        IsConnected && watch.ConnectionId == _hubConnection.ConnectionId
        && watch.ConnectionId == _windowPresenceConnectionId
        && watch.AuthenticationGeneration == _mizonApi.ReadAuthentication().Generation;

    private void ResetAccountPresence(AccountPresenceWatch? expected = null)
    {
        bool changed;
        lock (_windowPresenceGate)
        {
            if (expected is not null && !ReferenceEquals(_accountPresenceWatch, expected))
                return;

            changed = _accountPresence is not null;
            _accountPresence = null;
            _accountPresenceWatch = null;
        }
        if (changed)
            AccountPresenceChanged?.Invoke(this, AccountPresence);
    }

    private sealed record AccountPresenceWatch(Guid ClientPublicId, string ConnectionId, long AuthenticationGeneration);
}
