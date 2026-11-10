namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicAlbumMusicsOperation : MizonApiRequest<MusicAlbumMusicsRequest, MusicTabMusicsResponse>
{
    public MusicAlbumMusicsOperation(MusicAlbumMusicsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicAlbumMusics, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = MusicCatalogJsonContext.Default.BaseApiRequestMusicAlbumMusicsRequest;
        ResponseJsonTypeInfo = MusicCatalogJsonContext.Default.BaseApiResponseMusicTabMusicsResponse;
        MaximumClientCacheDuration = TimeSpan.FromMinutes(1);
    }
}
