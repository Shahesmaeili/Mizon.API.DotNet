namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicContributorMusicsOperation : MizonApiRequest<MusicContributorMusicsRequest, MusicTabMusicsResponse>
{
    public MusicContributorMusicsOperation(MusicContributorMusicsRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicContributorMusics, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromMinutes(1);
    }
}
