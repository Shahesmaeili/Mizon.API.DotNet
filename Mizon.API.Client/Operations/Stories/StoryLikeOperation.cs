using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;
namespace Mizon.API.Client.Operations.Stories;
public sealed class StoryLikeOperation : MizonApiRequest<StoryLikeRequest, StoryResponse>
{
    public StoryLikeOperation(StoryLikeRequest request) : base(HttpMethod.POST, Endpoints.Stories.StoryLike, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = StoryPlaybackJsonContext.Default.BaseApiRequestStoryLikeRequest;
        ResponseJsonTypeInfo = StoryPlaybackJsonContext.Default.BaseApiResponseStoryResponse;
    }
}
