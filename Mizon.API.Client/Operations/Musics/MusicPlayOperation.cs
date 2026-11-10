namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicPlayOperation : MizonApiRequest<MusicPlayRequest, MusicPlayResponse>
{
    public MusicPlayOperation(MusicPlayRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicPlay, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = FollowingActivitiesJsonContext.Default.BaseApiRequestMusicPlayRequest;
        ResponseJsonTypeInfo = FollowingActivitiesJsonContext.Default.BaseApiResponseMusicPlayResponse;
    }
}
