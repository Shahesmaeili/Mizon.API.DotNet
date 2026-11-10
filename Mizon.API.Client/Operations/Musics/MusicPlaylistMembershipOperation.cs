namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistMembershipOperation : MizonApiRequest<MusicPlaylistMembershipRequest, MusicPlaylistMembershipResponse>
{
    public MusicPlaylistMembershipOperation(MusicPlaylistMembershipRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylistMembership, request)
    {
        RequestJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiRequestMusicPlaylistMembershipRequest;
        ResponseJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiResponseMusicPlaylistMembershipResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
