using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicFileWaveformRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicFileWaveformResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicStemMixRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicStemMixResponse>))]
internal partial class MusicPlaybackJsonContext : JsonSerializerContext;
