namespace Mizon.API.Client.Requests.Stories;

public sealed class MusicStoriesRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
}
