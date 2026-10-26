namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
