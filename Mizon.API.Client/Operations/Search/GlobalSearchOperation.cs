namespace Mizon.API.Client.Operations.Search;

public sealed class GlobalSearchOperation : MizonApiStreamRequest<GlobalSearchRequest, GlobalSearchItemResponse>
{
    public GlobalSearchOperation(GlobalSearchRequest request)
        : base(HttpMethod.GET, Endpoints.Search.Global, request)
    {
        RequestQueryString = $"?Query={Uri.EscapeDataString(request.Query ?? string.Empty)}";
        ResponseJsonTypeInfo = SearchJsonContext.Default.GlobalSearchItemResponse;
    }
}
