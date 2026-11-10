namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicStemMixOperation : MizonApiRequest<MusicStemMixRequest, MusicStemMixResponse>
{
    public MusicStemMixOperation(MusicStemMixRequest request)
        : base(HttpMethod.GET, Endpoints.Musics.MusicStemMix, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = MusicPlaybackJsonContext.Default.BaseApiRequestMusicStemMixRequest;
        ResponseJsonTypeInfo = MusicPlaybackJsonContext.Default.BaseApiResponseMusicStemMixResponse;
        // MemoryCache does not accept a zero relative expiration. Keep this very short,
        // because a new analysis may make stems available while the app is open.
        MaximumClientCacheDuration = TimeSpan.FromSeconds(5);
    }
}
