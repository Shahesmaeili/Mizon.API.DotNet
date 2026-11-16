using Mizon.API.Client.Responses.Musics;

namespace Mizon.API.Client.Responses.Persons;

public sealed class PersonMusicsResponse : IApiResponse
{
    public List<MusicTabMusicItemResponse> Musics { get; set; } = [];
    public Guid? NextBeforeMusicPublicId { get; set; }
}
