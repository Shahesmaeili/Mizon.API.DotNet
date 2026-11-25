using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<ClientSessionsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<ClientSessionsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<TerminateSessionRequest>))]
[JsonSerializable(typeof(BaseApiRequest<TerminateOtherSessionsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<TerminateSessionsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<UpdateSessionInactivityRequest>))]
[JsonSerializable(typeof(BaseApiResponse<UpdateSessionInactivityResponse>))]
[JsonSerializable(typeof(BaseApiRequest<ConfirmSessionRequest>))]
[JsonSerializable(typeof(BaseApiResponse<ConfirmSessionResponse>))]
internal partial class ClientSessionsJsonContext : JsonSerializerContext;
