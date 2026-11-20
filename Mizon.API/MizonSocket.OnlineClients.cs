using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Mizon.API;

public sealed record OnlineClientCount(int Count, long Revision);

public sealed partial class MizonSocket
{
    private readonly Lock _onlineClientsGate = new();
    private OnlineClientCount? _onlineClientCount;

    public HubConnectionState ConnectionState => _hubConnection.State;
    public OnlineClientCount? OnlineClients => IsConnected ? Volatile.Read(ref _onlineClientCount) : null;
    public event EventHandler? OnlineClientsChanged;

    public Task<int> GetOnlineFollowerCountAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        return _hubConnection.InvokeAsync<int>("GetOnlineFollowerCount", token);
    }

    private void ApplyOnlineClientCount(OnlineClientCount count)
    {
        if (!IsConnected || count.Count < 0 || count.Revision < 0) return;
        lock (_onlineClientsGate)
        {
            if (!IsConnected || (_onlineClientCount is { } previous && count.Revision <= previous.Revision))
                return;
            Volatile.Write(ref _onlineClientCount, count);
        }
        OnlineClientsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetOnlineClientCount()
    {
        lock (_onlineClientsGate) Volatile.Write(ref _onlineClientCount, null);
        OnlineClientsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReadOnlineClientCountAsync(CancellationToken token)
    {
        try
        {
            if (await _hubConnection.InvokeAsync<OnlineClientCount>("GetOnlineClientCount", token).ConfigureAwait(false) is { } count)
                ApplyOnlineClientCount(count);
        }
        catch (HubException)
        {
            // Older deployments can still connect and use all other hub operations.
        }
    }
}
