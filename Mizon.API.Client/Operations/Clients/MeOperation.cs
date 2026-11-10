namespace Mizon.API.Client.Operations.Clients;

public sealed class MeOperation : MizonApiRequest<MeRequest, MeResponse>
{
    public MeOperation(MeRequest request) : base(HttpMethod.GET, Endpoints.Clients.ME, request)
    {
        // Current-account checks must reach the server, including after an account switch.
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = string.Empty;
        RequestJsonTypeInfo = SearchJsonContext.Default.BaseApiRequestMeRequest;
        ResponseJsonTypeInfo = SearchJsonContext.Default.BaseApiResponseMeResponse;
    }
}
