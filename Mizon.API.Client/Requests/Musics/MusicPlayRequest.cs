namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicPlayRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
    public Guid PlaybackPublicId { get; set; }
}
