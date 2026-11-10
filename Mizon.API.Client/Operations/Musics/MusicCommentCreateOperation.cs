namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicCommentCreateOperation : MizonApiRequest<MusicCommentCreateRequest, MusicCommentCreateResponse>
{
    public MusicCommentCreateOperation(MusicCommentCreateRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicCommentCreate, request)
    {
        RequestJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiRequestMusicCommentCreateRequest;
        ResponseJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiResponseMusicCommentCreateResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
