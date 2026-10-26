namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicFileRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
