namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicCommentTagSearchOperation : MizonApiRequest<MusicCommentTagSearchRequest, MusicCommentTagSearchResponse>
{
    public MusicCommentTagSearchOperation(MusicCommentTagSearchRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicCommentTagSearch, request)
    {
        RequestJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiRequestMusicCommentTagSearchRequest;
        ResponseJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiResponseMusicCommentTagSearchResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = $"?Type={Uri.EscapeDataString(request.Type)}&Query={Uri.EscapeDataString(request.Query)}";
    }
}
