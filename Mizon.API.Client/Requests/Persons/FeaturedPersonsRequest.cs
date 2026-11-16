namespace Mizon.API.Client.Requests.Persons;

public sealed class FeaturedPersonsRequest : IApiRequest
{
    public string? TypeCode { get; init; }
}
