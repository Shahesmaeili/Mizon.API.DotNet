namespace Mizon.API.Client.Responses.Musics;

public sealed class MusicStemMixResponse : IApiResponse
{
    public bool IsAvailable { get; set; }
    public Guid? MusicFilePublicId { get; set; }
    public Guid? VocalsMediaPublicId { get; set; }
    public Guid? InstrumentalMediaPublicId { get; set; }
}
