using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicCommentsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicCommentsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicCommentCreateRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicCommentCreateResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicCommentVoteRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicCommentVoteResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicCommentTagSearchRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicCommentTagSearchResponse>))]
internal partial class MusicCommentsJsonContext : JsonSerializerContext;
