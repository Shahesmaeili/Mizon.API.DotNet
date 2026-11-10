namespace Mizon.API.Client.Operations.Clients;

public sealed class ClientProfileOperation : MizonApiRequest<ClientProfileRequest, ClientProfileResponse>
{
    public ClientProfileOperation(ClientProfileRequest request)
        : base(HttpMethod.GET, Endpoints.Clients.ClientProfile, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = $"?ClientPublicId={request.ClientPublicId:D}";
        RequestJsonTypeInfo = SearchJsonContext.Default.BaseApiRequestClientProfileRequest;
        ResponseJsonTypeInfo = SearchJsonContext.Default.BaseApiResponseClientProfileResponse;
    }
}
