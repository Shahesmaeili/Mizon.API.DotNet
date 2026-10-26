namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicLyricOperation : MizonApiRequest<MusicLyricRequest, MusicLyricResponse>
{
    public MusicLyricOperation(MusicLyricRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicLyric, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}