namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicStemMixRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
