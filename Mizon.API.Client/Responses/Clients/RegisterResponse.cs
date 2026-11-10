namespace Mizon.API.Client.Responses.Clients;

public sealed class RegisterResponse : IApiResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public Guid SessionPublicId { get; set; }
    public Guid ClientPublicId { get; set; }
}
