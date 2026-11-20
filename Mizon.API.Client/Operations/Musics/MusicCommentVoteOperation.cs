namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicCommentVoteOperation : MizonApiRequest<MusicCommentVoteRequest, MusicCommentVoteResponse>
{
    public MusicCommentVoteOperation(MusicCommentVoteRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicCommentVote, request)
    {
        RequestJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiRequestMusicCommentVoteRequest;
        ResponseJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiResponseMusicCommentVoteResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
