namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistDetailsOperation : MizonApiRequest<MusicPlaylistDetailsRequest, MusicPlaylistDetailsResponse>
{
    public MusicPlaylistDetailsOperation(MusicPlaylistDetailsRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylistDetails, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiRequestMusicPlaylistDetailsRequest;
        ResponseJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiResponseMusicPlaylistDetailsResponse;
    }
}

