using Microsoft.Extensions.Caching.Memory;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mizon.API;


public sealed class MizonApi
{


    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IMemoryCache _memoryCache;
    private string? _token;
    private Version? _osVersion;
    private string? _deviceIdentifier;
    private string? _deviceModel;
    private string? _deviceName;
    private string? _deviceUsername;
    private Guid? _platformVersion;

    public MizonApi(HttpClient httpClient,
        IMemoryCache memoryCache)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
    }

    public void SetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));

        _token = token;
    }

    public void SetDeviceInformation(Version OsVersion, string deviceIdentifier, string deviceModel, string deviceName, string deviceUsername, Guid platformVersion)
    {
        _osVersion = OsVersion;
        _deviceIdentifier = deviceIdentifier;
        _deviceModel = deviceModel;
        _deviceName = deviceName;
        _deviceUsername = deviceUsername;
        _platformVersion = platformVersion;

    }

    public string? GetToken() => _token;

    public async IAsyncEnumerable<Response> SendStreamRequestAsync<Request, Response>(
        MizonApiStreamRequest<Request, Response> mizonApiRequest,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        where Request : IApiRequest
        where Response : IApiResponse
    {
        using var httpRequestMessage = new HttpRequestMessage();
        ManageHttpVersion(httpRequestMessage);
        ManageAuthorization(httpRequestMessage, mizonApiRequest.NeedAuthorized, _token);
        ManageInformationHeaders(httpRequestMessage, _osVersion, _deviceIdentifier, _deviceModel, _deviceName, _deviceUsername, _platformVersion);

        if (mizonApiRequest.HttpMethod != HttpMethod.GET)
            throw new NotSupportedException("Only GET is supported for streamed API requests.");

        httpRequestMessage.RequestUri = new Uri($"{mizonApiRequest.Endpoint}{BuildQueryString(mizonApiRequest.BaseApiRequest.Content)}");
        httpRequestMessage.Method = System.Net.Http.HttpMethod.Get;

        using var timeoutCts = new CancellationTokenSource(mizonApiRequest.CallTimeoutDuration);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
        using var response = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
        using var reader = new StreamReader(responseStream, Encoding.UTF8, leaveOpen: false);

        while (await reader.ReadLineAsync(linkedCts.Token).ConfigureAwait(false) is { Length: > 0 } line)
        {
            var item = JsonSerializer.Deserialize<Response>(line, JsonOptions);
            if (item is not null)
                yield return item;
        }
    }



    public async ValueTask<BaseApiResponse<Response>> SendRequestAsync<Request, Response>(MizonApiRequest<Request, Response> mizonApiRequest, CancellationToken? cancellationToken = null)
        where Request : IApiRequest where Response : IApiResponse
    {
        var baseApiResponse = new BaseApiResponse<Response>();

        try
        {
            var cacheKey = ManageCacheKey(mizonApiRequest.BaseApiRequest, mizonApiRequest.Endpoint, mizonApiRequest.HttpMethod);

            var cachedData = ManageGetCache(cacheKey, mizonApiRequest.ClientCacheStrategy);

            if (cachedData is BaseApiResponse<Response> cachedResponse)
            {
                cachedResponse.IsFromCache = true;
                return cachedResponse;
            }

            var httpRequestMessage = new HttpRequestMessage();

            ManageHttpVersion(httpRequestMessage);

            ManageAuthorization(httpRequestMessage, mizonApiRequest.NeedAuthorized, _token);

            ManageInformationHeaders(httpRequestMessage, _osVersion, _deviceIdentifier, _deviceModel, _deviceName, _deviceUsername, _platformVersion);


            if (mizonApiRequest.HttpMethod == HttpMethod.GET)
            {
                var query = BuildQueryString(mizonApiRequest.BaseApiRequest.Content);

                httpRequestMessage.RequestUri = new($"{mizonApiRequest.Endpoint}{query}");
                httpRequestMessage.Method = System.Net.Http.HttpMethod.Get;
            }
            else if (mizonApiRequest.HttpMethod == HttpMethod.POST)
            {
                httpRequestMessage.RequestUri = new(mizonApiRequest.Endpoint);
                httpRequestMessage.Method = System.Net.Http.HttpMethod.Post;

                string requestJson = JsonSerializer.Serialize(mizonApiRequest.BaseApiRequest, JsonOptions);
                ManageCompression(httpRequestMessage, requestJson, mizonApiRequest.CompressionMethod);
            }
            else throw new NotImplementedException();

            using var timeoutCts = new CancellationTokenSource(mizonApiRequest.CallTimeoutDuration);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken ?? CancellationToken.None);

            var httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, linkedCts.Token);

            if (httpResponseMessage.StatusCode is HttpStatusCode.Unauthorized)
            {
                baseApiResponse.Error = new BaseApiError
                {
                    Code = 401,
                    Title = "Unauthorized",
                };

                return baseApiResponse;
            }

            var responseJson = await httpResponseMessage.Content.ReadAsStringAsync();

            var response = JsonSerializer.Deserialize<BaseApiResponse<Response>>(responseJson, JsonOptions);

            ManageToken(mizonApiRequest.PropertyForToken, response);
            ManageSetCache(cacheKey, response, mizonApiRequest.ClientCacheStrategy, mizonApiRequest.MaximumClientCacheDuration);


            return response!;
        }
        catch (SocketException ex)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 101,
                Title = "Network Error",
                Details = "A socket error occurred while trying to send the request. " + ex.Message
            };
        }
        catch (IOException ex)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 102,
                Title = "I/O Error",
                Details = "An I/O error occurred during request processing. " + ex.Message
            };
        }
        catch (JsonException ex)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 103,
                Title = "Invalid JSON",
                Details = "Failed to parse the JSON response. " + ex.Message
            };
        }
        catch (TimeoutException ex)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 104,
                Title = "Timeout",
                Details = "The request operation timed out.. " + ex.Message
            };
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 105,
                Title = "Cancelled by User",
                Details = "The request was cancelled by the caller.. " + ex.Message
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 106,
                Title = "Request Timeout",
                Details = "The request was automatically cancelled due to timeout. " + ex.Message
            };
        }
        catch (HttpRequestException ex)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 107,
                Title = "HTTP Error",
                Details = "An HTTP error occurred while sending the request. " + ex.Message
            };
        }
        catch (Exception ex)
        {
            baseApiResponse.Error = new BaseApiError
            {
                Code = 108,
                Title = "Unexpected Error",
                Details = "An unexpected error occurred. " + ex.Message
            };
        }

        return baseApiResponse;
    }

    // تابع برای تولید کلید منحصر به فرد برای کش
    private string ManageCacheKey(object data, string endpoint, HttpMethod httpMethod)
    {
        var json = JsonSerializer.Serialize(data);
        return ComputeHash($"{endpoint}_{httpMethod}_{json}");
    }

    private string ComputeHash(string input)
    {
        Span<byte> buffer = stackalloc byte[32]; // SHA256 = 32 bytes
        SHA256.HashData(Encoding.UTF8.GetBytes(input), buffer);
        return Convert.ToHexString(buffer);
    }


    // تابع برای بررسی درخواست تکراری
    private bool IsDuplicateRequest(string cacheKey)
    {
        // بررسی وجود کلید درخواست در کش
        return _memoryCache.TryGetValue(cacheKey, out _);
    }


    private void ManageAuthorization(HttpRequestMessage httpRequestMessage, bool needAuthorized, string? token = null)
    {
        if (needAuthorized)
        {
            if (!string.IsNullOrEmpty(token))
                httpRequestMessage.Headers.Add("Authorization", $"Bearer {token}");
            else throw new Exception("This method need authorized!");
        }
    }

    private void ManageInformationHeaders(HttpRequestMessage httpRequestMessage, Version? OsVersion, string? deviceIdentifier, string? deviceModel, string? deviceName, string? deviceUsername, Guid? platformVersion)
    {
        httpRequestMessage.Headers.Add("Mizon-OS-Version", OsVersion?.ToString());
        httpRequestMessage.Headers.Add("Mizon-Device-Identifier", deviceIdentifier);
        httpRequestMessage.Headers.Add("Mizon-Device-Model", deviceModel);
        httpRequestMessage.Headers.Add("Mizon-Device-Name", deviceName);
        httpRequestMessage.Headers.Add("Mizon-Device-Username", deviceUsername);
        httpRequestMessage.Headers.Add("Mizon-Platform-Version", platformVersion?.ToString());
    }

    private void ManageToken<TApiResponse>(Func<TApiResponse, string>? propertyForToken, BaseApiResponse<TApiResponse>? baseApiResponse) where TApiResponse : IApiResponse
    {
        if (propertyForToken is null)
            return;

        if (baseApiResponse is null)
            return;

        if (baseApiResponse.Content is null)
            return;

        _token = propertyForToken(baseApiResponse.Content!);
    }

    private void ManageHttpVersion(HttpRequestMessage httpRequestMessage)
    {
        httpRequestMessage.Version = HttpVersion.Version30;
        httpRequestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }

    private void ManageCompression(HttpRequestMessage httpRequestMessage, string value, CompressionMethod compressionMethod)
    {
        using var memoryStream = new MemoryStream();

        var jsonBytes = Encoding.UTF8.GetBytes(value);

        if (compressionMethod is CompressionMethod.None)
        {
            memoryStream.Write(jsonBytes);
        }
        else if (compressionMethod is CompressionMethod.GZip)
        {
            using var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress);
            gZipStream.Write(jsonBytes);
        }
        else if (compressionMethod is CompressionMethod.Deflate)
        {
            using var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress);
            deflateStream.Write(jsonBytes);
        }

        var byteArrayContent = new ByteArrayContent(memoryStream.ToArray());
        byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (compressionMethod is CompressionMethod.GZip)
            byteArrayContent.Headers.ContentEncoding.Add("gzip");

        else if (compressionMethod is CompressionMethod.Deflate)
            byteArrayContent.Headers.ContentEncoding.Add("deflate");

        else byteArrayContent.Headers.ContentEncoding.Clear();

        httpRequestMessage.Content = byteArrayContent;
    }

    private static string BuildQueryString<T>(T? obj)
    {
        if (obj == null)
            return "";

        var properties = typeof(T).GetProperties();

        var query = new List<string>();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            if (value == null)
                continue;

            query.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(value.ToString()!)}");
        }

        return query.Count > 0 ? "?" + string.Join("&", query) : "";
    }

    private void ManageSetCache(string cacheKey, object? data, ClientCacheStrategy clientCacheStrategy, TimeSpan? maximumClientCacheDuration = null)
    {
        if (clientCacheStrategy is ClientCacheStrategy.Disabled)
            return;

        if (data is null)
            return;

        var memoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = maximumClientCacheDuration ?? TimeSpan.FromMinutes(5)
        };

        _memoryCache.Set(cacheKey, data, memoryCacheEntryOptions);
    }

    private object? ManageGetCache(string cacheKey, ClientCacheStrategy clientCacheStrategy)
    {
        if (clientCacheStrategy is not ClientCacheStrategy.Disabled)
        {
            if (_memoryCache.TryGetValue(cacheKey, out var cachedResponse))
            {
                if (clientCacheStrategy is ClientCacheStrategy.Enabled ||
                    (clientCacheStrategy is ClientCacheStrategy.EnabledOnDuplicateRequest && IsDuplicateRequest(cacheKey)))
                {
                    return cachedResponse;
                }
            }
        }

        return null;
    }

}
