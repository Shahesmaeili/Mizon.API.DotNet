namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicGenreTypesOperation : MizonApiRequest<MusicGenreTypesRequest, MusicGenreTypesResponse>
{
    public MusicGenreTypesOperation(MusicGenreTypesRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicGenreTypes, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromMinutes(5);
    }
}
