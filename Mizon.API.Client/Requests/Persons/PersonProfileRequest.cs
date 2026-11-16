namespace Mizon.API.Client.Requests.Persons;

public sealed class PersonProfileRequest : IApiRequest
{
    public Guid PersonPublicId { get; set; }
}
