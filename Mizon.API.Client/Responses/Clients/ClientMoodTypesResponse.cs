namespace Mizon.API.Client.Responses.Clients;

public sealed class ClientMoodTypesResponse : IApiResponse
{
    public List<ClientMoodItemResponse> Items { get; set; } = [];
}

