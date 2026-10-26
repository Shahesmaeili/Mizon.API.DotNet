namespace Mizon.API;

/// <summary>
/// Represents a request to the Mizon API.
/// </summary>
/// <typeparam name="TApiRequest">The type of the API request data. Must implement <see cref="IApiRequest"/>.</typeparam>
/// <typeparam name="TApiResponse">The type of the API response data. Must implement <see cref="IApiResponse"/>.</typeparam>
public class MizonApiRequest<TApiRequest, TApiResponse>
    where TApiRequest : IApiRequest
    where TApiResponse : IApiResponse
{

    /// <summary>
    /// The underlying base API request object.
    /// It is initialized with the provided request data.
    /// </summary>
    protected internal BaseApiRequest<TApiRequest> BaseApiRequest { get; init; } = new();


    /// <summary>
    /// Indicates whether the request requires authorization.
    /// Defaults to true.
    /// </summary>
    protected internal bool NeedAuthorized { get; init; } = true;

    protected internal Func<TApiResponse, string>? PropertyForToken { get; init; }

    /// <summary>
    /// The client-side caching strategy to use.
    /// Defaults to EnabledOnDuplicateRequest.
    /// </summary>
    protected internal ClientCacheStrategy ClientCacheStrategy { get; init; } = ClientCacheStrategy.EnabledOnDuplicateRequest;


    /// <summary>
    /// The maximum time that a cached response is considered valid.
    /// </summary>
    protected internal TimeSpan? MaximumClientCacheDuration { get; init; }
    protected internal object? Token { get; init; }

    /// <summary>
    /// The request timeout duration.
    /// Defaults to 10 seconds.
    /// </summary>
    protected internal TimeSpan CallTimeoutDuration { get; init; } = TimeSpan.FromSeconds(10);


    /// <summary>
    /// The compression method to use for the request body.
    /// Defaults to GZip.
    /// </summary>
    protected internal CompressionMethod CompressionMethod { get; init; } = CompressionMethod.GZip;


    /// <summary>
    /// The HTTP method to use for the request.
    /// </summary>
    protected internal HttpMethod HttpMethod { get; private set; }


    /// <summary>
    /// The API endpoint to target.
    /// </summary>
    protected internal string Endpoint { get; private set; }


    /// <summary>
    /// Represents a request to the Mizon API.
    /// </summary>
    /// <param name="httpMethod">The HTTP method to use for the request.</param>
    /// <param name="endpoint">The API endpoint to target.</param>
    /// <param name="apiRequest">The data to send with the request.</param>
    public MizonApiRequest(HttpMethod httpMethod, string endpoint, TApiRequest apiRequest)
    {
        BaseApiRequest.Content = apiRequest;
        HttpMethod = httpMethod;
        Endpoint = endpoint;
    }
}
