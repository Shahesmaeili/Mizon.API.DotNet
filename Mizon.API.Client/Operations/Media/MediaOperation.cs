namespace Mizon.API.Client.Operations.Media;

public sealed class MediaOperation : MizonApiRequest<Requests.Media.MediaRequest, Responses.Media.MediaResponse>
{
    public MediaOperation(Requests.Media.MediaRequest request) : base(HttpMethod.GET, Endpoints.Medias.Media, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}
