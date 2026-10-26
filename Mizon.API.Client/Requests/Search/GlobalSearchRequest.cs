namespace Mizon.API.Client.Requests.Search;

public sealed class GlobalSearchRequest : IApiRequest
{
    public string Query { get; set; } = string.Empty;
}
