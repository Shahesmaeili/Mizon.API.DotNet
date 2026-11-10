using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<FollowingActivitiesRequest>))]
[JsonSerializable(typeof(BaseApiResponse<FollowingActivitiesResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicPlayRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicPlayResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicViewRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicViewResponse>))]
internal partial class FollowingActivitiesJsonContext : JsonSerializerContext;
