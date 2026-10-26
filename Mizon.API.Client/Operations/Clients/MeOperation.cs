namespace Mizon.API.Client.Operations.Clients;

public sealed class MeOperation : MizonApiRequest<MeRequest, MeResponse>
{
    public MeOperation(MeRequest request) : base(HttpMethod.GET, Endpoints.Clients.ME, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}
