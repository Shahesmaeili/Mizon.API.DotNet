using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GlobalSearchItemResponse))]
[JsonSerializable(typeof(BaseApiRequest<MeRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MeResponse>))]
[JsonSerializable(typeof(BaseApiRequest<ClientProfileRequest>))]
[JsonSerializable(typeof(BaseApiResponse<ClientProfileResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicFileRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicFileResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicResponse>))]
internal partial class SearchJsonContext : JsonSerializerContext;
