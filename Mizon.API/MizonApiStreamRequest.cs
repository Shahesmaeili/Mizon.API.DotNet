namespace Mizon.API;

public class MizonApiStreamRequest<TApiRequest, TApiResponse>
    where TApiRequest : IApiRequest
    where TApiResponse : IApiResponse
{
    protected internal BaseApiRequest<TApiRequest> BaseApiRequest { get; init; } = new();
    protected internal System.Text.Json.Serialization.Metadata.JsonTypeInfo<BaseApiRequest<TApiRequest>>? RequestJsonTypeInfo { get; init; }
    protected internal System.Text.Json.Serialization.Metadata.JsonTypeInfo<TApiResponse>? ResponseJsonTypeInfo { get; init; }
    protected internal string? RequestQueryString { get; init; }
    protected internal bool NeedAuthorized { get; init; } = true;
    protected internal TimeSpan CallTimeoutDuration { get; init; } = TimeSpan.FromSeconds(10);
    protected internal HttpMethod HttpMethod { get; }
    protected internal string Endpoint { get; }

    public MizonApiStreamRequest(HttpMethod httpMethod, string endpoint, TApiRequest apiRequest)
    {
        BaseApiRequest.Content = apiRequest;
        HttpMethod = httpMethod;
        Endpoint = endpoint;
    }
}
