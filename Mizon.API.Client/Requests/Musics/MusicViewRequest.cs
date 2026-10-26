namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicViewRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
