using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;
namespace Mizon.API.Client.Operations.Stories;
public sealed class StoryViewOperation : MizonApiRequest<StoryRequest, StoryResponse> { public StoryViewOperation(StoryRequest request) : base(HttpMethod.POST, Endpoints.Stories.StoryView, request) { ClientCacheStrategy = ClientCacheStrategy.Disabled; } }
