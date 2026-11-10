namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicReactionsOperation : MizonApiRequest<MusicReactionsRequest, MusicReactionsResponse>
{
    public MusicReactionsOperation(MusicReactionsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicReactions, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = MusicReactionsJsonContext.Default.BaseApiRequestMusicReactionsRequest;
        ResponseJsonTypeInfo = MusicReactionsJsonContext.Default.BaseApiResponseMusicReactionsResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}