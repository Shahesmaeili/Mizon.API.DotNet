namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicOperation : MizonApiRequest<MusicRequest, MusicResponse>
{
    public MusicOperation(MusicRequest request) : base(HttpMethod.GET, Endpoints.Musics.MUSIC, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = SearchJsonContext.Default.BaseApiRequestMusicRequest;
        ResponseJsonTypeInfo = SearchJsonContext.Default.BaseApiResponseMusicResponse;
    }
}
