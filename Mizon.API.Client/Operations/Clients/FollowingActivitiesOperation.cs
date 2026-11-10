namespace Mizon.API.Client.Operations.Clients;

public sealed class FollowingActivitiesOperation : MizonApiRequest<FollowingActivitiesRequest, FollowingActivitiesResponse>
{
    public FollowingActivitiesOperation(FollowingActivitiesRequest request)
        : base(HttpMethod.POST, Endpoints.Clients.FollowingActivities, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = FollowingActivitiesJsonContext.Default.BaseApiRequestFollowingActivitiesRequest;
        ResponseJsonTypeInfo = FollowingActivitiesJsonContext.Default.BaseApiResponseFollowingActivitiesResponse;
    }
}
