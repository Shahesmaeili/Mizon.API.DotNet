using Mizon.API.Client.Requests.Persons;
using Mizon.API.Client.Responses.Persons;

namespace Mizon.API.Client.Operations.Persons;

public sealed class PersonMusicsOperation : MizonApiRequest<PersonMusicsRequest, PersonMusicsResponse>
{
    public PersonMusicsOperation(PersonMusicsRequest request)
        : base(HttpMethod.GET, Endpoints.Persons.PersonMusics, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
        RequestQueryString = request.BeforeMusicPublicId is Guid before
            ? $"?PersonPublicId={request.PersonPublicId:D}&BeforeMusicPublicId={before:D}"
            : $"?PersonPublicId={request.PersonPublicId:D}";
        RequestJsonTypeInfo = PersonJsonContext.Default.BaseApiRequestPersonMusicsRequest;
        ResponseJsonTypeInfo = PersonJsonContext.Default.BaseApiResponsePersonMusicsResponse;
    }
}
