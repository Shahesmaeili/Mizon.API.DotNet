namespace Mizon.API.Client.Requests.Media;

public sealed class MediaRequest : IApiRequest
{
    public Guid MediaPublicId { get; set; }
}
