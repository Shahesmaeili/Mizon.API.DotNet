namespace Mizon.API.Client.Requests.Clients;

public sealed class ClientMoodRequest : IApiRequest
{
    public Guid? ClientMoodTypePublicId { get; set; }
}

