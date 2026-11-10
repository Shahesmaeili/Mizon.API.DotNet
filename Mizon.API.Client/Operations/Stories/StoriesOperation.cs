using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;
namespace Mizon.API.Client.Operations.Stories;
public sealed class StoriesOperation : MizonApiRequest<StoriesRequest, StoriesResponse>
{
    public StoriesOperation() : base(HttpMethod.GET, Endpoints.Stories.List, new())
    {
        RequestQueryString = string.Empty;
        RequestJsonTypeInfo = StoryPlaybackJsonContext.Default.BaseApiRequestStoriesRequest;
        ResponseJsonTypeInfo = StoryPlaybackJsonContext.Default.BaseApiResponseStoriesResponse;
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
