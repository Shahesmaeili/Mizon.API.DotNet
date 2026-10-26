namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicCommentsOperation : MizonApiRequest<MusicCommentsRequest, MusicCommentsResponse>
{
    public MusicCommentsOperation(MusicCommentsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicComments, request)
    {
        // یک کامنت تازه باید بلافاصله marker خودش را روی waveform نشان بدهد.
        // پاسخ این endpoint به‌دلیل درج هم‌زمان کاربر cache نمی‌شود.
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
