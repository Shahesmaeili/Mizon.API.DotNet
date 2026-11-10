namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicContributorsOperation : MizonApiRequest<MusicContributorRequest, MusicContributorsResponse>
{
    public MusicContributorsOperation(MusicContributorRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicContributors, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = MusicCatalogJsonContext.Default.BaseApiRequestMusicContributorRequest;
        ResponseJsonTypeInfo = MusicCatalogJsonContext.Default.BaseApiResponseMusicContributorsResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}
