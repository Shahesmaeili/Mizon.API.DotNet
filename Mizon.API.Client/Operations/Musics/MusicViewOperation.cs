namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicViewOperation : MizonApiRequest<Requests.Musics.MusicViewRequest, Responses.Musics.MusicViewResponse>
{
    public MusicViewOperation(Requests.Musics.MusicViewRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicView, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
