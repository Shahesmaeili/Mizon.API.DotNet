namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicAlbumMusicsOperation : MizonApiRequest<MusicAlbumMusicsRequest, MusicTabMusicsResponse>
{
    public MusicAlbumMusicsOperation(MusicAlbumMusicsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicAlbumMusics, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromMinutes(1);
    }
}
