namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicStemMixOperation : MizonApiRequest<MusicStemMixRequest, MusicStemMixResponse>
{
    public MusicStemMixOperation(MusicStemMixRequest request)
        : base(HttpMethod.GET, Endpoints.Musics.MusicStemMix, request)
    {
        // MemoryCache does not accept a zero relative expiration. Keep this very short,
        // because a new analysis may make stems available while the app is open.
        MaximumClientCacheDuration = TimeSpan.FromSeconds(5);
    }
}
