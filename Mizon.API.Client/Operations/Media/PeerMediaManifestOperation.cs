using Mizon.API.Client.Requests.Media;
using Mizon.API.Client.Responses.Media;

namespace Mizon.API.Client.Operations.Media;

public sealed class PeerMediaManifestOperation : MizonApiRequest<MediaRequest, PeerMediaManifestResponse>
{
    public PeerMediaManifestOperation(MediaRequest request) : base(HttpMethod.GET, Endpoints.Medias.PeerManifest, request)
    {
        RequestQueryString = $"?MediaPublicId={request.MediaPublicId:D}";
        RequestJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiRequestMediaRequest;
        ResponseJsonTypeInfo = MusicLibraryJsonContext.Default.BaseApiResponsePeerMediaManifestResponse;
        CallTimeoutDuration = TimeSpan.FromSeconds(2);
        MaximumClientCacheDuration = TimeSpan.FromSeconds(30);
    }
}
