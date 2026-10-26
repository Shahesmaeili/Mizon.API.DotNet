namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicReactionsOperation : MizonApiRequest<MusicReactionsRequest, MusicReactionsResponse>
{
    public MusicReactionsOperation(MusicReactionsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicReactions, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}