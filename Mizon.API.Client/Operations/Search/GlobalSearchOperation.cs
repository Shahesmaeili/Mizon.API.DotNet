using Mizon.API.Client.Requests.Search;
using Mizon.API.Client.Responses.Search;

namespace Mizon.API.Client.Operations.Search;

public sealed class GlobalSearchOperation : MizonApiStreamRequest<GlobalSearchRequest, GlobalSearchItemResponse>
{
    public GlobalSearchOperation(GlobalSearchRequest request)
        : base(HttpMethod.GET, Endpoints.Search.Global, request)
    {
    }
}
