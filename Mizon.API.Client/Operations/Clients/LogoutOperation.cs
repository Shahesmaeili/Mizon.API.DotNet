namespace Mizon.API.Client.Operations.Clients;

public sealed class LogoutOperation : MizonApiRequest<LogoutRequest, LogoutResponse>
{
    public LogoutOperation(LogoutRequest request) : base(HttpMethod.POST, Endpoints.Clients.LOGOUT, request)
    {
        RequestJsonTypeInfo = AuthenticationJsonContext.Default.BaseApiRequestLogoutRequest;
        ResponseJsonTypeInfo = AuthenticationJsonContext.Default.BaseApiResponseLogoutResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}