namespace Mizon.API.Client.Responses.Musics;

public sealed class MusicCommentTagSearchResponse : IApiResponse
{
    public List<MusicCommentTagItem> Tags { get; set; } = [];
}
