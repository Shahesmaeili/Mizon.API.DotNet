namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicReactionRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
    public Guid MusicReactionTypePublicId { get; set; }
    public bool Reacted { get; set; }
}
