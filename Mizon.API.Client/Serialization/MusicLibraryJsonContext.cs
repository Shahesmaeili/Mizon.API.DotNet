using System.Text.Json.Serialization;
using Mizon.API.Client.Requests.Media;
using Mizon.API.Client.Responses.Media;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicLibraryRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicLibraryResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicPlaylistDetailsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicPlaylistDetailsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicPlaylistsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicPlaylistsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicPlaylistCreateRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicPlaylistCreateResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicPlaylistMembershipRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicPlaylistMembershipResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MediaRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MediaResponse>))]
internal partial class MusicLibraryJsonContext : JsonSerializerContext;

