namespace Mizon.API;

/// <summary>Combined active-window state for the authenticated account.</summary>
public sealed class ClientAccountPresenceResponse
{
    public Guid ClientPublicId { get; init; }
    public bool IsOnline { get; init; }
    public long Revision { get; init; }
}
