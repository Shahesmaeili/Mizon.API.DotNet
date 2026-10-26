using Mizon.API.Client.Requests.Musics;
using Mizon.API.Client.Responses.Musics;

namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistMembershipOperation : MizonApiRequest<MusicPlaylistMembershipRequest, MusicPlaylistMembershipResponse>
{
    public MusicPlaylistMembershipOperation(MusicPlaylistMembershipRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylistMembership, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
