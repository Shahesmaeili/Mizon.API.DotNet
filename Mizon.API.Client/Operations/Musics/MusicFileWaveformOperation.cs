namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicFileWaveformOperation : MizonApiRequest<MusicFileWaveformRequest, MusicFileWaveformResponse>
{
    public MusicFileWaveformOperation(MusicFileWaveformRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicFileWaveform, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}