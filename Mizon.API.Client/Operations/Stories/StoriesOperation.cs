using Mizon.API.Client.Requests.Stories;
using Mizon.API.Client.Responses.Stories;
namespace Mizon.API.Client.Operations.Stories;
public sealed class StoriesOperation : MizonApiRequest<StoriesRequest, StoriesResponse> { public StoriesOperation() : base(HttpMethod.GET, Endpoints.Stories.List, new()) { } }
