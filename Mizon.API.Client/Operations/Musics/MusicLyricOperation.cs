namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicLyricOperation : MizonApiRequest<MusicLyricRequest, MusicLyricResponse>
{
    public MusicLyricOperation(MusicLyricRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicLyric, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = MusicLyricsJsonContext.Default.BaseApiRequestMusicLyricRequest;
        ResponseJsonTypeInfo = MusicLyricsJsonContext.Default.BaseApiResponseMusicLyricResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}