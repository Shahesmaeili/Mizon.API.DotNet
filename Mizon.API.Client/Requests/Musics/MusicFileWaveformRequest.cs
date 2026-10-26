namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicFileWaveformRequest : IApiRequest
{
    public Guid MusicFilePublicId { get; set; }
}
