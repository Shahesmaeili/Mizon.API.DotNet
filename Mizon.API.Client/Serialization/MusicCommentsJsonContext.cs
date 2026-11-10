using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicCommentsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicCommentsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicCommentCreateRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicCommentCreateResponse>))]
internal partial class MusicCommentsJsonContext : JsonSerializerContext;
