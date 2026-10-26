namespace Mizon.API.Client.Operations.Clients;

/// <summary>
/// Represents login operation.
/// </summary>
public sealed class LoginOperation : MizonApiRequest<LoginRequest, LoginResponse>
{
    public LoginOperation(LoginRequest request) : base(HttpMethod.POST, Endpoints.Clients.LOGIN, request)
    {
        //Set the NeedAuthorized property to false to indicate that this method doesn't require authorization.
        NeedAuthorized = false;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(5);

        //Set the PropertyForToken property to a lambda expression that extracts the token from the login response.
        PropertyForToken = (response) => response.AccessToken;
    }
}
