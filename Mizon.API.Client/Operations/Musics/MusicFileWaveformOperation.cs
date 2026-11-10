namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicFileWaveformOperation : MizonApiRequest<MusicFileWaveformRequest, MusicFileWaveformResponse>
{
    public MusicFileWaveformOperation(MusicFileWaveformRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicFileWaveform, request)
    {
        RequestQueryString = $"?MusicFilePublicId={request.MusicFilePublicId:D}";
        RequestJsonTypeInfo = MusicPlaybackJsonContext.Default.BaseApiRequestMusicFileWaveformRequest;
        ResponseJsonTypeInfo = MusicPlaybackJsonContext.Default.BaseApiResponseMusicFileWaveformResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}