namespace Mizon.API.Client.Operations.Clients;

public sealed class ClientMoodTypesOperation : MizonApiRequest<ClientMoodTypesRequest, ClientMoodTypesResponse>
{
    public ClientMoodTypesOperation(ClientMoodTypesRequest request) : base(HttpMethod.GET, Endpoints.Clients.ClientMoodTypes, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = string.Empty;
        RequestJsonTypeInfo = ClientMoodJsonContext.Default.BaseApiRequestClientMoodTypesRequest;
        ResponseJsonTypeInfo = ClientMoodJsonContext.Default.BaseApiResponseClientMoodTypesResponse;
    }
}

