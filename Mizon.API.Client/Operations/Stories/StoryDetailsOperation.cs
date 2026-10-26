using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;

namespace Mizon.API.Client.Operations.Stories;

public sealed class StoryDetailsOperation : MizonApiRequest<StoryRequest, StoryResponse>
{
    public StoryDetailsOperation(StoryRequest request) : base(HttpMethod.GET, Endpoints.Stories.StoryDetails, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
