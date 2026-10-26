namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicContributorsOperation : MizonApiRequest<MusicContributorRequest, MusicContributorsResponse>
{
    public MusicContributorsOperation(MusicContributorRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicContributors, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}
