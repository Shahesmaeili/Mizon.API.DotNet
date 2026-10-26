using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;

namespace Mizon.API.Client.Operations.Stories;

public sealed class MusicStoriesOperation : MizonApiRequest<MusicStoriesRequest, StoriesResponse>
{
    public MusicStoriesOperation(MusicStoriesRequest request) : base(HttpMethod.GET, Endpoints.Stories.MusicStories, request)
    {
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}
