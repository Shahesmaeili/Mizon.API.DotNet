using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;

namespace Mizon.API.Client.Operations.Stories;

public sealed class MusicStoriesOperation : MizonApiRequest<MusicStoriesRequest, StoriesResponse>
{
    public MusicStoriesOperation(MusicStoriesRequest request) : base(HttpMethod.GET, Endpoints.Stories.MusicStories, request)
    {
        RequestQueryString = $"?MusicPublicId={request.MusicPublicId:D}";
        RequestJsonTypeInfo = StoryPlaybackJsonContext.Default.BaseApiRequestMusicStoriesRequest;
        ResponseJsonTypeInfo = StoryPlaybackJsonContext.Default.BaseApiResponseStoriesResponse;
        MaximumClientCacheDuration = TimeSpan.FromSeconds(10);
    }
}
