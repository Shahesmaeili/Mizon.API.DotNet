namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicFeaturesRequest : IApiRequest
{
    public int Limit { get; set; } = 12;
}
