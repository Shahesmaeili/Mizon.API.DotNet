using Mizon.API.Client.Requests.Persons;
using Mizon.API.Client.Responses.Persons;

namespace Mizon.API.Client.Operations.Persons;

public sealed class FeaturedPersonsOperation : MizonApiRequest<FeaturedPersonsRequest, FeaturedPersonsResponse>
{
    public FeaturedPersonsOperation(FeaturedPersonsRequest request)
        : base(HttpMethod.GET, Endpoints.Persons.FeaturedPersons, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = $"?TypeCode={Uri.EscapeDataString(request.TypeCode ?? string.Empty)}";
        RequestJsonTypeInfo = PersonJsonContext.Default.BaseApiRequestFeaturedPersonsRequest;
        ResponseJsonTypeInfo = PersonJsonContext.Default.BaseApiResponseFeaturedPersonsResponse;
    }
}
