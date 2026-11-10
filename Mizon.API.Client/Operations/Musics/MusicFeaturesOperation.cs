namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicFeaturesOperation : MizonApiRequest<MusicFeaturesRequest, MusicFeaturesResponse>
{
    public MusicFeaturesOperation(MusicFeaturesRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicFeatures, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = MusicFeaturesJsonContext.Default.BaseApiRequestMusicFeaturesRequest;
        ResponseJsonTypeInfo = MusicFeaturesJsonContext.Default.BaseApiResponseMusicFeaturesResponse;
    }
}
