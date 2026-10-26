namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicContributorRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
