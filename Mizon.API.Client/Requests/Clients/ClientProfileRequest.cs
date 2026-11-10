namespace Mizon.API.Client.Requests.Clients;

public sealed class ClientProfileRequest : IApiRequest
{
    public Guid ClientPublicId { get; set; }
}
