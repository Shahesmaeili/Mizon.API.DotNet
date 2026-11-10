namespace Mizon.API.Client.Responses.Clients;

public sealed class ClientMoodResponse : IApiResponse
{
    public ClientMoodItemResponse? CurrentMood { get; set; }
}

