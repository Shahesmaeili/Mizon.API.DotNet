namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicLikeOperation : MizonApiRequest<MusicLikeRequest, MusicLikeResponse>
{
    public MusicLikeOperation(MusicLikeRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicLike, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = MusicFeaturesJsonContext.Default.BaseApiRequestMusicLikeRequest;
        ResponseJsonTypeInfo = MusicFeaturesJsonContext.Default.BaseApiResponseMusicLikeResponse;
    }
}
