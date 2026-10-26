using Mizon.API.Client.Requests.Musics;
using Mizon.API.Client.Responses.Musics;

namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistsOperation : MizonApiRequest<MusicPlaylistsRequest, MusicPlaylistsResponse>
{
    public MusicPlaylistsOperation(MusicPlaylistsRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylists, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
