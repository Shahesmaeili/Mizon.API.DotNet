namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicPlaylistsRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
