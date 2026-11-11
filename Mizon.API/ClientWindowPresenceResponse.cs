namespace Mizon.API;

/// <summary>Server-confirmed state of one authenticated main-window connection.</summary>
public sealed class ClientWindowPresenceResponse
{
    public Guid ClientPublicId { get; init; }
    public string ConnectionId { get; init; } = string.Empty;
    public bool IsOnline { get; init; }
    public long Revision { get; init; }
}
