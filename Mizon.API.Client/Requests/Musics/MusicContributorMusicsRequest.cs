namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicContributorMusicsRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
