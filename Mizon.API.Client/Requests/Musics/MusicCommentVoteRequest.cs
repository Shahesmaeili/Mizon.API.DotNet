namespace Mizon.API.Client.Requests.Musics;

public sealed class MusicCommentVoteRequest : IApiRequest
{
    public Guid MusicPublicId { get; set; }
    public Guid MusicCommentPublicId { get; set; }
    public int Vote { get; set; }
}
