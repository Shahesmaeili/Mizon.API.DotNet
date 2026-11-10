namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistsOperation : MizonApiRequest<MusicPlaylistsRequest, MusicPlaylistsResponse>
{
    public MusicPlaylistsOperation(MusicPlaylistsRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylists, request)
    {
        RequestJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiRequestMusicPlaylistsRequest;
        ResponseJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiResponseMusicPlaylistsResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
