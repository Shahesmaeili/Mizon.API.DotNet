namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistCreateOperation : MizonApiRequest<MusicPlaylistCreateRequest, MusicPlaylistCreateResponse>
{
    public MusicPlaylistCreateOperation(MusicPlaylistCreateRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylistCreate, request)
    {
        RequestJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiRequestMusicPlaylistCreateRequest;
        ResponseJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiResponseMusicPlaylistCreateResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
