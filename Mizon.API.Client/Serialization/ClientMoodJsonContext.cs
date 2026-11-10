using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<ClientMoodTypesRequest>))]
[JsonSerializable(typeof(BaseApiResponse<ClientMoodTypesResponse>))]
[JsonSerializable(typeof(BaseApiRequest<ClientMoodRequest>))]
[JsonSerializable(typeof(BaseApiResponse<ClientMoodResponse>))]
internal partial class ClientMoodJsonContext : JsonSerializerContext;

