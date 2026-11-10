namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicCommentsRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
    public Guid? MusicCommentPublicId { get; set; }
}
