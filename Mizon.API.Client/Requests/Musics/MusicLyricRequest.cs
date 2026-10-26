namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicLyricRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
