namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicLibraryOperation : MizonApiRequest<MusicLibraryRequest, MusicLibraryResponse>
{
    public MusicLibraryOperation(MusicLibraryRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicLibrary, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiRequestMusicLibraryRequest;
        ResponseJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiResponseMusicLibraryResponse;
    }
}

