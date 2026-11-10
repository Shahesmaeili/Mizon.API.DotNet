namespace Mizon.API.Client.Operations.Clients;

public sealed class ClientMoodOperation : MizonApiRequest<ClientMoodRequest, ClientMoodResponse>
{
    public ClientMoodOperation(ClientMoodRequest request) : base(HttpMethod.POST, Endpoints.Clients.ClientMood, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = ClientMoodJsonContext.Default.BaseApiRequestClientMoodRequest;
        ResponseJsonTypeInfo = ClientMoodJsonContext.Default.BaseApiResponseClientMoodResponse;
    }
}

