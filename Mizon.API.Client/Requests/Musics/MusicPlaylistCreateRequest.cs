namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicPlaylistCreateRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
    public string Title { get; set; } = null!;
}
