using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<LoginRequest>))]
[JsonSerializable(typeof(BaseApiResponse<LoginResponse>))]
[JsonSerializable(typeof(BaseApiRequest<RegisterRequest>))]
[JsonSerializable(typeof(BaseApiResponse<RegisterResponse>))]
[JsonSerializable(typeof(BaseApiRequest<LogoutRequest>))]
[JsonSerializable(typeof(BaseApiResponse<LogoutResponse>))]
internal partial class AuthenticationJsonContext : JsonSerializerContext;
