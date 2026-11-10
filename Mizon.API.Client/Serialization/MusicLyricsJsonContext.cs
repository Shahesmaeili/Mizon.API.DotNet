using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicLyricRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicLyricResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicLyricLineRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicLyricLineResponse>))]
internal partial class MusicLyricsJsonContext : JsonSerializerContext;
