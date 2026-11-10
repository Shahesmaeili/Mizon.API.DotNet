using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicFeaturesRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicFeaturesResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicLikeRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicLikeResponse>))]
internal partial class MusicFeaturesJsonContext : JsonSerializerContext;
