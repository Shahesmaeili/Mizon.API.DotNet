namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicCommentsOperation : MizonApiRequest<MusicCommentsRequest, MusicCommentsResponse>
{
    public MusicCommentsOperation(MusicCommentsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicComments, request)
    {
        RequestJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiRequestMusicCommentsRequest;
        // یک کامنت تازه باید بلافاصله marker خودش را روی waveform نشان بدهد.
        // پاسخ این endpoint به‌دلیل درج هم‌زمان کاربر cache نمی‌شود.
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}"
            + (request.MusicCommentPublicId is Guid commentId ? $"&MusicCommentPublicId={commentId:D}" : string.Empty);
        ResponseJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiResponseMusicCommentsResponse;
    }
}
