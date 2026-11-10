namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicLyricLineOperation : MizonApiRequest<MusicLyricLineRequest, MusicLyricLineResponse>
{
    public MusicLyricLineOperation(MusicLyricLineRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicLyricLine, request)
    {
        RequestQueryString = $"?MusicLyricPublicId={request.MusicLyricPublicId:D}";
        RequestJsonTypeInfo = MusicLyricsJsonContext.Default.BaseApiRequestMusicLyricLineRequest;
        ResponseJsonTypeInfo = MusicLyricsJsonContext.Default.BaseApiResponseMusicLyricLineResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}