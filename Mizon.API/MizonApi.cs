using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mizon.API;

public sealed class MizonApi : IDisposable
{
    private static long _nextCacheScope;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly bool _ownsHttpClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private SessionState _session = new(null, Interlocked.Increment(ref _nextCacheScope));
    private DeviceInformation? _device;
    private int _disposed;

    public MizonApi(HttpClient httpClient, IMemoryCache memoryCache) : this(httpClient, memoryCache, ownsHttpClient: false)
    {
    }

    public MizonApi(HttpClient httpClient, IMemoryCache memoryCache, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(memoryCache);
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _ownsHttpClient = ownsHttpClient;
        _lifetimeToken = _lifetimeCancellation.Token;
    }

    public void SetToken(string token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        while (true)
        {
            var session = Volatile.Read(ref _session);
            if (session.Token == token) return;
            var replacement = new SessionState(token, Interlocked.Increment(ref _nextCacheScope));
            if (ReferenceEquals(Interlocked.CompareExchange(ref _session, replacement, session), session)) return;
        }
    }

    public string? GetToken() => Volatile.Read(ref _session).Token;

    public void ClearToken() => Interlocked.Exchange(ref _session,
        new SessionState(null, Interlocked.Increment(ref _nextCacheScope)));

    public void SetDeviceInformation(Version OsVersion, string deviceIdentifier, string deviceModel, string deviceName, string deviceUsername, Guid platformVersion)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Volatile.Write(ref _device, new DeviceInformation(OsVersion.ToString(), deviceIdentifier, deviceModel,
            deviceName, deviceUsername, platformVersion.ToString(), Interlocked.Increment(ref _nextCacheScope)));
    }

    public async IAsyncEnumerable<Response> SendStreamRequestAsync<Request, Response>(
        MizonApiStreamRequest<Request, Response> mizonApiRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where Request : IApiRequest where Response : IApiResponse
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(mizonApiRequest);
        cancellationToken.ThrowIfCancellationRequested();
        if (mizonApiRequest.HttpMethod != HttpMethod.GET)
            throw new NotSupportedException("Only GET is supported for streamed API requests.");

        var query = mizonApiRequest.RequestQueryString ?? BuildQueryString(mizonApiRequest.BaseApiRequest, mizonApiRequest.RequestJsonTypeInfo);
        using var request = CreateRequest(System.Net.Http.HttpMethod.Get, mizonApiRequest.Endpoint + query,
            mizonApiRequest.NeedAuthorized, Volatile.Read(ref _session), Volatile.Read(ref _device));
        using var requestLifetime = CreateRequestLifetime(cancellationToken, mizonApiRequest.CallTimeoutDuration);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestLifetime.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(requestLifetime.Token).ConfigureAwait(false);
        var typeInfo = ResolveTypeInfo(mizonApiRequest.ResponseJsonTypeInfo);

        // The server sends NDJSON. Parse UTF-8 incrementally without creating a string per result.
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable(stream, typeInfo, topLevelValues: true,
            requestLifetime.Token).ConfigureAwait(false))
        {
            requestLifetime.Token.ThrowIfCancellationRequested();
            if (item is not null) yield return item;
        }
    }

    public async ValueTask<BaseApiResponse<Response>> SendRequestAsync<Request, Response>(
        MizonApiRequest<Request, Response> mizonApiRequest, CancellationToken? cancellationToken = null)
        where Request : IApiRequest where Response : IApiResponse
    {
        var callerToken = cancellationToken.GetValueOrDefault();
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ArgumentNullException.ThrowIfNull(mizonApiRequest);
            callerToken.ThrowIfCancellationRequested();
            var session = Volatile.Read(ref _session);
            var device = Volatile.Read(ref _device);
            var isGet = mizonApiRequest.HttpMethod == HttpMethod.GET;
            var query = isGet ? mizonApiRequest.RequestQueryString ?? BuildQueryString(mizonApiRequest.BaseApiRequest, mizonApiRequest.RequestJsonTypeInfo) : string.Empty;
            var endpoint = mizonApiRequest.Endpoint + query;
            if (mizonApiRequest.NeedAuthorized && string.IsNullOrEmpty(session.Token))
                throw new InvalidOperationException("This method needs authorization.");
            if (!isGet && mizonApiRequest.HttpMethod != HttpMethod.POST)
                throw new NotSupportedException("Only GET and POST are supported for API requests.");

            // Reuse exactly the same UTF-8 body for the cache key and POST payload.
            var requestBytes = isGet ? null : JsonSerializer.SerializeToUtf8Bytes(mizonApiRequest.BaseApiRequest,
                ResolveTypeInfo(mizonApiRequest.RequestJsonTypeInfo));
            var cacheDuration = mizonApiRequest.MaximumClientCacheDuration ?? TimeSpan.FromMinutes(5);
            var useCache = mizonApiRequest.ClientCacheStrategy != ClientCacheStrategy.Disabled && cacheDuration > TimeSpan.Zero;
            var cacheKey = new ResponseCacheKey(session.CacheScope, device?.CacheScope ?? 0, endpoint,
                mizonApiRequest.HttpMethod, mizonApiRequest.NeedAuthorized, typeof(Response), useCache && requestBytes is not null ? Hash(requestBytes) : string.Empty);
            if (useCache && _memoryCache.TryGetValue<CachedResponse<Response>>(cacheKey, out var cached) && cached is not null)
                return new BaseApiResponse<Response> { Content = cached.Content, IsFromCache = true };

            using var request = CreateRequest(isGet ? System.Net.Http.HttpMethod.Get : System.Net.Http.HttpMethod.Post,
                endpoint, mizonApiRequest.NeedAuthorized, session, device);
            if (requestBytes is not null)
                request.Content = CreateContent(requestBytes, mizonApiRequest.CompressionMethod);
            using var requestLifetime = CreateRequestLifetime(callerToken, mizonApiRequest.CallTimeoutDuration);
            using var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestLifetime.Token).ConfigureAwait(false);
            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                return Failure<Response>(401, "Unauthorized", "The server rejected the access token.");

            await using var stream = await httpResponse.Content.ReadAsStreamAsync(requestLifetime.Token).ConfigureAwait(false);
            var response = await JsonSerializer.DeserializeAsync(stream, ResolveTypeInfo(mizonApiRequest.ResponseJsonTypeInfo), requestLifetime.Token).ConfigureAwait(false)
                ?? throw new JsonException("The API returned a null response envelope.");
            callerToken.ThrowIfCancellationRequested();
            if (!httpResponse.IsSuccessStatusCode && response.IsSuccess)
                httpResponse.EnsureSuccessStatusCode();

            if (response.IsSuccess && response.Content is not null && mizonApiRequest.PropertyForToken is { } tokenSelector)
            {
                var token = tokenSelector(response.Content);
                ArgumentException.ThrowIfNullOrWhiteSpace(token);
                // A slow login must not restore a token after logout or overwrite a newer login.
                Interlocked.CompareExchange(ref _session,
                    new SessionState(token, Interlocked.Increment(ref _nextCacheScope)), session);
            }
            if (useCache && response.IsSuccess && ReferenceEquals(session, Volatile.Read(ref _session)) && ReferenceEquals(device, Volatile.Read(ref _device)))
                _memoryCache.Set(cacheKey, new CachedResponse<Response>(response.Content), cacheDuration);
            return response;
        }
        catch (OperationCanceledException ex) when (callerToken.IsCancellationRequested)
        {
            return Failure<Response>(105, "Cancelled by User", "The request was cancelled by the caller. " + ex.Message);
        }
        catch (OperationCanceledException ex) when (Volatile.Read(ref _disposed) != 0)
        {
            return Failure<Response>(105, "Cancelled", "The API client was disposed. " + ex.Message);
        }
        catch (OperationCanceledException ex)
        {
            return Failure<Response>(106, "Request Timeout", "The request was automatically cancelled due to timeout. " + ex.Message);
        }
        catch (SocketException ex)
        {
            return Failure<Response>(101, "Network Error", "A socket error occurred while trying to send the request. " + ex.Message);
        }
        catch (IOException ex)
        {
            return Failure<Response>(102, "I/O Error", "An I/O error occurred during request processing. " + ex.Message);
        }
        catch (JsonException ex)
        {
            return Failure<Response>(103, "Invalid JSON", "Failed to parse the JSON response. " + ex.Message);
        }
        catch (TimeoutException ex)
        {
            return Failure<Response>(104, "Timeout", "The request operation timed out. " + ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return Failure<Response>(107, "HTTP Error", "An HTTP error occurred while sending the request. " + ex.Message);
        }
        catch (Exception ex)
        {
            return Failure<Response>(108, "Unexpected Error", "An unexpected error occurred. " + ex.Message);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The legacy untrimmed-JIT fallback is guarded by JsonSerializer.IsReflectionEnabledByDefault. Trimmed/AOT consumers use source-generated metadata; the SDK supplies it for every operation.")]
    private static JsonTypeInfo<T> ResolveTypeInfo<T>(JsonTypeInfo<T>? typeInfo)
    {
        if (typeInfo is not null) return typeInfo;
        if (!RuntimeFeature.IsDynamicCodeSupported || !JsonSerializer.IsReflectionEnabledByDefault)
            throw new NotSupportedException("This operation must provide source-generated JSON metadata when reflection is disabled.");
        // Compatibility for existing operations in untrimmed JIT apps. Built-in operations always supply metadata.
        return (JsonTypeInfo<T>)JsonSerializerOptions.Web.GetTypeInfo(typeof(T));
    }

    private static string BuildQueryString<T>(BaseApiRequest<T> request, JsonTypeInfo<BaseApiRequest<T>>? typeInfo) where T : IApiRequest
    {
        // A metadata-based fallback for external operations; built-in operations provide typed queries.
        var json = JsonSerializer.SerializeToElement(request, ResolveTypeInfo(typeInfo));
        if (!json.TryGetProperty("content", out var content) && !json.TryGetProperty("Content", out content)) return string.Empty;
        if (content.ValueKind == JsonValueKind.Null) return string.Empty;
        var values = new List<string>();
        foreach (var property in content.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null) continue;
            values.Add($"{Uri.EscapeDataString(property.Name)}={Uri.EscapeDataString(property.Value.ToString())}");
        }
        return values.Count == 0 ? string.Empty : "?" + string.Join('&', values);
    }

    private CancellationTokenSource CreateRequestLifetime(CancellationToken token, TimeSpan timeout)
    {
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetimeToken);
        try
        {
            lifetime.CancelAfter(timeout);
            return lifetime;
        }
        catch
        {
            lifetime.Dispose();
            throw;
        }
    }

    private static HttpRequestMessage CreateRequest(System.Net.Http.HttpMethod method, string endpoint, bool needAuthorized, SessionState session, DeviceInformation? device)
    {
        if (needAuthorized && string.IsNullOrEmpty(session.Token))
            throw new InvalidOperationException("This method needs authorization.");
        var request = new HttpRequestMessage(method, endpoint)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        try
        {
            if (needAuthorized) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            if (device is not null)
            {
                request.Headers.Add("Mizon-OS-Version", device.OsVersion);
                request.Headers.Add("Mizon-Device-Identifier", device.Identifier);
                request.Headers.Add("Mizon-Device-Model", device.Model);
                request.Headers.Add("Mizon-Device-Name", device.Name);
                request.Headers.Add("Mizon-Device-Username", device.Username);
                request.Headers.Add("Mizon-Platform-Version", device.PlatformVersion);
            }
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    private static HttpContent CreateContent(byte[] json, CompressionMethod compression)
    {
        if (compression == CompressionMethod.None)
        {
            var content = new ByteArrayContent(json);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return content;
        }
        using var buffer = new MemoryStream();
        using (Stream compressor = compression switch
        {
            CompressionMethod.GZip => new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true),
            CompressionMethod.Deflate => new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(compression))
        })
            compressor.Write(json);
        // ByteArrayContent owns the buffer reference until the request has finished; avoid ToArray's copy.
        var result = new ByteArrayContent(buffer.GetBuffer(), 0, checked((int)buffer.Length));
        result.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        result.Headers.ContentEncoding.Add(compression == CompressionMethod.GZip ? "gzip" : "deflate");
        return result;
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash);
    }

    private static BaseApiResponse<T> Failure<T>(int code, string title, string details) where T : IApiResponse =>
        new() { Error = new BaseApiError { Code = code, Title = title, Details = details } };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        ClearToken();
        try { _lifetimeCancellation.Cancel(); }
        finally
        {
            _lifetimeCancellation.Dispose();
            if (_ownsHttpClient) _httpClient.Dispose();
        }
    }

    private sealed record SessionState(string? Token, long CacheScope);
    private sealed record DeviceInformation(string OsVersion, string Identifier, string Model, string Name, string Username, string PlatformVersion, long CacheScope);
    private sealed record CachedResponse<T>(T? Content) where T : IApiResponse;
    private readonly record struct ResponseCacheKey(long Session, long Device, string Endpoint, HttpMethod Method, bool Authorized, Type ResponseType, string BodyHash);
}
