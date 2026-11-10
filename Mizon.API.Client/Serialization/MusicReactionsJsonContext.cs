using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicReactionRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicReactionResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicReactionsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicReactionsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicReactionTypesRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicReactionTypesResponse>))]
internal partial class MusicReactionsJsonContext : JsonSerializerContext;
