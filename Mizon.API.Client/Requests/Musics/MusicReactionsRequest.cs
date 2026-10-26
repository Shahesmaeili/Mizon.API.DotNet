namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicReactionsRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
