namespace Mizon.API.Client.Operations.Clients;

public sealed class RegisterOperation : MizonApiRequest<RegisterRequest, RegisterResponse>
{
    public RegisterOperation(RegisterRequest request) : this(request, updateToken: true)
    {
    }

    public RegisterOperation(RegisterRequest request, bool updateToken) : base(HttpMethod.POST, Endpoints.Clients.REGISTER, request)
    {
        RequestJsonTypeInfo = AuthenticationJsonContext.Default.BaseApiRequestRegisterRequest;
        ResponseJsonTypeInfo = AuthenticationJsonContext.Default.BaseApiResponseRegisterResponse;
        NeedAuthorized = false;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;

        if (updateToken)
            PropertyForToken = response => response.AccessToken;
    }
}
