using Mizon.API.Client.Requests.Persons;
using Mizon.API.Client.Responses.Persons;

namespace Mizon.API.Client.Operations.Persons;

public sealed class PersonFollowOperation : MizonApiRequest<PersonFollowRequest, PersonFollowResponse>
{
    public PersonFollowOperation(PersonFollowRequest request)
        : base(HttpMethod.POST, Endpoints.Persons.PersonFollow, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestJsonTypeInfo = PersonJsonContext.Default.BaseApiRequestPersonFollowRequest;
        ResponseJsonTypeInfo = PersonJsonContext.Default.BaseApiResponsePersonFollowResponse;
    }
}
