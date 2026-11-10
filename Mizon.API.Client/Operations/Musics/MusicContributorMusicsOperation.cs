namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicContributorMusicsOperation : MizonApiRequest<MusicContributorMusicsRequest, MusicTabMusicsResponse>
{
    public MusicContributorMusicsOperation(MusicContributorMusicsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicContributorMusics, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = MusicCatalogJsonContext.Default.BaseApiRequestMusicContributorMusicsRequest;
        ResponseJsonTypeInfo = MusicCatalogJsonContext.Default.BaseApiResponseMusicTabMusicsResponse;
        MaximumClientCacheDuration = TimeSpan.FromMinutes(1);
    }
}
