namespace Mizon.API.Client.Operations.Clients;

public sealed class LogoutOperation : MizonApiRequest<LogoutRequest, LogoutResponse>
{
    public LogoutOperation(LogoutRequest request) : base(HttpMethod.POST, Endpoints.Clients.LOGOUT, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(5);
    }
}