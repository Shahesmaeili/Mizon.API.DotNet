using Mizon.API.Client.Requests.Musics;
using Mizon.API.Client.Responses.Musics;

namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlaylistCreateOperation : MizonApiRequest<MusicPlaylistCreateRequest, MusicPlaylistCreateResponse>
{
    public MusicPlaylistCreateOperation(MusicPlaylistCreateRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlaylistCreate, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
