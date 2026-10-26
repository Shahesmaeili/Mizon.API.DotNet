namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicLyricLineOperation : MizonApiRequest<MusicLyricLineRequest, MusicLyricLineResponse>
{
    public MusicLyricLineOperation(MusicLyricLineRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicLyricLine, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}