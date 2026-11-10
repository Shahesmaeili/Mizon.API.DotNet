namespace Mizon.API.Client.Operations.Clients;

/// <summary>
/// Represents login operation.
/// </summary>
public sealed class LoginOperation : MizonApiRequest<LoginRequest, LoginResponse>
{
    public LoginOperation(LoginRequest request) : this(request, updateToken: true)
    {
    }

    public LoginOperation(LoginRequest request, bool updateToken) : base(HttpMethod.POST, Endpoints.Clients.LOGIN, request)
    {
        //Set the NeedAuthorized property to false to indicate that this method doesn't require authorization.
        NeedAuthorized = false;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = AuthenticationJsonContext.Default.BaseApiRequestLoginRequest;
        ResponseJsonTypeInfo = AuthenticationJsonContext.Default.BaseApiResponseLoginResponse;

        //Set the PropertyForToken property to a lambda expression that extracts the token from the login response.
        // Callers with a cancellable UI can commit the token themselves after checking their lifetime.
        if (updateToken)
            PropertyForToken = (response) => response.AccessToken;
    }
}
