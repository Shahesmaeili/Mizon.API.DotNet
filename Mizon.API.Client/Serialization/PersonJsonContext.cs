using System.Text.Json.Serialization;
using Mizon.API.Client.Requests.Persons;
using Mizon.API.Client.Responses.Persons;

namespace Mizon.API.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<PersonProfileRequest>))]
[JsonSerializable(typeof(BaseApiResponse<PersonProfileResponse>))]
[JsonSerializable(typeof(BaseApiRequest<PersonMusicsRequest>))]
[JsonSerializable(typeof(BaseApiResponse<PersonMusicsResponse>))]
[JsonSerializable(typeof(BaseApiRequest<PersonFollowRequest>))]
[JsonSerializable(typeof(BaseApiResponse<PersonFollowResponse>))]
internal partial class PersonJsonContext : JsonSerializerContext;
