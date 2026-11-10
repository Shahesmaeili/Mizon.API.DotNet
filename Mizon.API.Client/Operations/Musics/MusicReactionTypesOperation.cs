namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicReactionTypesOperation : MizonApiRequest<MusicReactionTypesRequest, MusicReactionTypesResponse>
{
    public MusicReactionTypesOperation(MusicReactionTypesRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicReactionTypes, request)
    {
        RequestQueryString = string.Empty;
        RequestJsonTypeInfo = MusicReactionsJsonContext.Default.BaseApiRequestMusicReactionTypesRequest;
        ResponseJsonTypeInfo = MusicReactionsJsonContext.Default.BaseApiResponseMusicReactionTypesResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(60);
    }
}