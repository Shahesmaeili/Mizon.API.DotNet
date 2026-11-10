using System.Text.Json.Serialization;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<MusicAlbumMusicsRequest>))]
[JsonSerializable(typeof(BaseApiRequest<MusicContributorMusicsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicTabMusicsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicContributorRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicContributorsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<MusicGenreTypesRequest>))]
[JsonSerializable(typeof(BaseApiResponse<MusicGenreTypesResponse>))]
internal partial class MusicCatalogJsonContext : JsonSerializerContext;
