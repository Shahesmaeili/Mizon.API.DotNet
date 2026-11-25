namespace Mizon.API.Client.Operations.Clients;

public sealed class ClientSessionsOperation : MizonApiRequest<ClientSessionsRequest, ClientSessionsResponse>
{
    public ClientSessionsOperation(ClientSessionsRequest request) : base(HttpMethod.GET, Endpoints.Clients.Sessions, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiRequestClientSessionsRequest;
        ResponseJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiResponseClientSessionsResponse;
    }
}

public sealed class TerminateSessionOperation : MizonApiRequest<TerminateSessionRequest, TerminateSessionsResponse>
{
    public TerminateSessionOperation(TerminateSessionRequest request) : base(HttpMethod.POST, Endpoints.Clients.TerminateSession, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiRequestTerminateSessionRequest;
        ResponseJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiResponseTerminateSessionsResponse;
    }
}

public sealed class TerminateOtherSessionsOperation : MizonApiRequest<TerminateOtherSessionsRequest, TerminateSessionsResponse>
{
    public TerminateOtherSessionsOperation(TerminateOtherSessionsRequest request) : base(HttpMethod.POST, Endpoints.Clients.TerminateOtherSessions, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiRequestTerminateOtherSessionsRequest;
        ResponseJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiResponseTerminateSessionsResponse;
    }
}

public sealed class UpdateSessionInactivityOperation : MizonApiRequest<UpdateSessionInactivityRequest, UpdateSessionInactivityResponse>
{
    public UpdateSessionInactivityOperation(UpdateSessionInactivityRequest request) : base(HttpMethod.POST, Endpoints.Clients.UpdateSessionInactivity, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiRequestUpdateSessionInactivityRequest;
        ResponseJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiResponseUpdateSessionInactivityResponse;
    }
}

public sealed class ConfirmSessionOperation : MizonApiRequest<ConfirmSessionRequest, ConfirmSessionResponse>
{
    public ConfirmSessionOperation(ConfirmSessionRequest request) : base(HttpMethod.POST, Endpoints.Clients.ConfirmSession, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiRequestConfirmSessionRequest;
        ResponseJsonTypeInfo = ClientSessionsJsonContext.Default.BaseApiResponseConfirmSessionResponse;
    }
}
