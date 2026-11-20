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
            + (request.MusicCommentPublicId is Guid commentId ? $"&MusicCommentPublicId={commentId:D}" : string.Empty)
            + (request.ParentMusicCommentPublicId is Guid parent ? $"&ParentMusicCommentPublicId={parent:D}" : string.Empty)
            + (request.After is Guid after ? $"&After={after:D}" : string.Empty)
            + (request.PageSize is int size ? FormattableString.Invariant($"&PageSize={size}") : string.Empty)
            + (request.Sort is { } sort ? $"&Sort={Uri.EscapeDataString(sort)}" : string.Empty)
            + (request.TimedOnly == true ? "&TimedOnly=true" : string.Empty);
        ResponseJsonTypeInfo = MusicCommentsJsonContext.Default.BaseApiResponseMusicCommentsResponse;
    }
}
