using System.Text.Json.Serialization;
using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<StoriesRequest>))]
[JsonSerializable(typeof(BaseApiRequest<MusicStoriesRequest>))]
[JsonSerializable(typeof(BaseApiResponse<StoriesResponse>))]
[JsonSerializable(typeof(BaseApiRequest<StoryCreateRequest>))]
[JsonSerializable(typeof(BaseApiRequest<StoryRequest>))]
[JsonSerializable(typeof(BaseApiRequest<StoryLikeRequest>))]
[JsonSerializable(typeof(BaseApiResponse<StoryResponse>))]
internal partial class StoryPlaybackJsonContext : JsonSerializerContext;
