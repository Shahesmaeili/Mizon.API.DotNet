namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicFileOperation : MizonApiRequest<MusicFileRequest, MusicFileResponse>
{
    public MusicFileOperation(MusicFileRequest request) : base(HttpMethod.GET, Endpoints.Musics.MusicFile, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = SearchJsonContext.Default.BaseApiRequestMusicFileRequest;
        ResponseJsonTypeInfo = SearchJsonContext.Default.BaseApiResponseMusicFileResponse;
    }
}
