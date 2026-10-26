using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;

namespace Mizon.API.Client.Operations.Stories;

public sealed class StoryCreateOperation : MizonApiRequest<StoryCreateRequest, StoryResponse>
{
    public StoryCreateOperation(StoryCreateRequest request) : base(HttpMethod.POST, Endpoints.Stories.StoryCreate, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
