using Mizon.API.Client.Requests.Persons;
using Mizon.API.Client.Responses.Persons;

namespace Mizon.API.Client.Operations.Persons;

public sealed class PersonProfileOperation : MizonApiRequest<PersonProfileRequest, PersonProfileResponse>
{
    public PersonProfileOperation(PersonProfileRequest request)
        : base(HttpMethod.GET, Endpoints.Persons.PersonProfile, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = $"?PersonPublicId={request.PersonPublicId:D}";
        RequestJsonTypeInfo = PersonJsonContext.Default.BaseApiRequestPersonProfileRequest;
        ResponseJsonTypeInfo = PersonJsonContext.Default.BaseApiResponsePersonProfileResponse;
    }
}
