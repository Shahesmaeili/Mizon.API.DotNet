namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicCommentCreateOperation : MizonApiRequest<MusicCommentCreateRequest, MusicCommentCreateResponse>
{
    public MusicCommentCreateOperation(MusicCommentCreateRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicCommentCreate, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
