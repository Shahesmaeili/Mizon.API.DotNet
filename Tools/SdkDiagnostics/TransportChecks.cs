using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Mizon.API;
using Mizon.API.Client;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiMethod = Mizon.API.HttpMethod;

internal static class TransportChecks
{
    public static async Task RunAsync()
    {
        using var handler = new FakeHandler();
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var api = new MizonApi(http, cache);
        api.SetToken("one");
        var first = await api.SendRequestAsync(new TestOperation(cache: true));
        var second = await api.SendRequestAsync(new TestOperation(cache: true));
        Check(first.IsSuccess && !first.IsFromCache && second.IsFromCache && !ReferenceEquals(first, second), "cache does not mutate the original envelope");
        Check(handler.Count == 1, "one cache lookup/request identity");
        first.Content = new TestResponse { Text = "caller replacement" };
        Check((await api.SendRequestAsync(new TestOperation(cache: true))).Content?.Text == "one", "cache envelope is isolated from caller replacement");
        api.SetToken("two");
        Check((await api.SendRequestAsync(new TestOperation(cache: true))).Content?.Text == "two" && handler.Count == 2, "cache is partitioned by session");
        await api.SendRequestAsync(new TestOperation(cache: true, query: "?authorization-scope=1"));
        Check((await api.SendRequestAsync(new TestOperation(cache: true, query: "?authorization-scope=1", authorized: false))).Content?.Text == "anonymous", "public operations do not reuse an authorized response");
        await api.SendRequestAsync(new TestOperation(fallbackQuery: true));
        Check(handler.LastUri?.Query == "?Text=" + Uri.EscapeDataString("آهنگ & test"), "metadata query fallback supports PascalCase envelope");
        using (var other = new MizonApi(http, cache))
        {
            other.SetToken("other");
            Check((await other.SendRequestAsync(new TestOperation(cache: true))).Content?.Text == "other", "shared memory cache does not cross API instances");
        }
        api.SetDeviceInformation(new Version(10, 0), "id", "model", "device", "user", Guid.Empty);
        Check(!(await api.SendRequestAsync(new TestOperation(cache: true))).IsFromCache, "device metadata changes invalidate cache scope");
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            Check((await api.SendRequestAsync(new TestOperation(cache: true), canceled.Token)).Error?.Code == 105, "pre-cancel beats cached result");
        }
        foreach (var ttl in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-1) })
        {
            var calls = handler.Count;
            Check((await api.SendRequestAsync(new TestOperation(cache: true, ttl: ttl))).IsSuccess, "nonpositive cache TTL does not fail a request");
            Check(handler.Count == calls + 1, "nonpositive TTL disables caching");
        }
        foreach (var compression in new[] { CompressionMethod.None, CompressionMethod.GZip, CompressionMethod.Deflate })
        {
            var result = await api.SendRequestAsync(new TestOperation(post: true, compression: compression));
            Check(result.IsSuccess && handler.LastBodyText == "آهنگ & test", $"UTF-8 request and {compression} compression round-trip");
        }
        handler.Json = "{\"error\":{\"code\":400,\"title\":\"bad\"}}";
        var count = handler.Count;
        await api.SendRequestAsync(new TestOperation(cache: true, query: "?error=1"));
        await api.SendRequestAsync(new TestOperation(cache: true, query: "?error=1"));
        Check(handler.Count == count + 2, "business errors are not cached");
        handler.Json = "null";
        Check((await api.SendRequestAsync(new TestOperation())).Error?.Code == 103, "null JSON envelope is rejected");
        handler.Json = "{";
        Check((await api.SendRequestAsync(new TestOperation())).Error?.Code == 103, "invalid JSON classified");
        handler.Json = null;
        handler.Status = HttpStatusCode.Unauthorized;
        Check((await api.SendRequestAsync(new TestOperation())).Error?.Code == 401, "401 classified");
        handler.Status = HttpStatusCode.InternalServerError;
        Check((await api.SendRequestAsync(new TestOperation())).Error?.Code == 107, "HTTP failure cannot masquerade as success");
        handler.Status = HttpStatusCode.OK;
        handler.Wait = true;
        using (var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            Check((await api.SendRequestAsync(new TestOperation(), canceled.Token)).Error?.Code == 105, "linked caller cancellation stays caller cancellation");
        Check((await api.SendRequestAsync(new TestOperation(timeout: TimeSpan.FromMilliseconds(30)))).Error?.Code == 106, "request timeout classified");
        handler.Wait = false;

        handler.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var login = api.SendRequestAsync(new TestOperation(login: true)).AsTask();
        api.ClearToken();
        handler.Gate.SetResult();
        await login;
        Check(api.GetToken() is null, "late login cannot undo logout");
        handler.Gate = null;
        api.SetToken("active");
        handler.Json = "{\"content\":{\"text\":\"wrong-token\"},\"error\":{\"code\":400}}";
        await api.SendRequestAsync(new TestOperation(login: true));
        Check(api.GetToken() == "active", "failed login does not update token");
        handler.Json = null;

        await CheckStreamingAsync();
        await CheckDependencyInjectionAsync();
        Check(handler.ResponseStreams.All(s => s.WasDisposed), "response streams disposed on all normal/error paths");
        using var pendingHandler = new FakeHandler { Wait = true };
        using var pendingHttp = new HttpClient(pendingHandler);
        var pendingApi = new MizonApi(pendingHttp, cache);
        pendingApi.SetToken("pending");
        var pendingRequest = pendingApi.SendRequestAsync(new TestOperation()).AsTask();
        pendingApi.Dispose();
        pendingApi.Dispose();
        Check((await pendingRequest.WaitAsync(TimeSpan.FromSeconds(2))).Error?.Code == 105, "disposal cancels active requests with externally owned HttpClient");
        Console.WriteLine("Transport: cache isolation, compression, cancellation, errors, streaming, DI and ownership passed.");
    }

    private static async Task CheckStreamingAsync()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new StreamHandler();
        using var http = new HttpClient(handler);
        using var api = new MizonApi(http, cache);
        await using (var items = api.SendStreamRequestAsync(new StreamOperation()).GetAsyncEnumerator())
        {
            Check(await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)), "first NDJSON item arrives before response completes");
            Check(items.Current.Text == "first", "first NDJSON row parsed");
            Check(!handler.Stream!.Tail.Task.IsCompleted, "no whole-response buffering");
            handler.Stream.Tail.TrySetResult();
            Check(await items.MoveNextAsync() && items.Current.Text == "second", "second NDJSON row parsed");
            Check(!await items.MoveNextAsync(), "NDJSON completes");
        }
        Check(handler.Stream!.WasDisposed, "completed stream disposed");
        await using (var items = api.SendStreamRequestAsync(new StreamOperation()).GetAsyncEnumerator())
            Check(await items.MoveNextAsync(), "early enumeration can stop after one item");
        Check(handler.Stream!.WasDisposed, "early enumeration disposes HTTP body");
        using var cancellation = new CancellationTokenSource();
        await using (var items = api.SendStreamRequestAsync(new StreamOperation(), cancellation.Token).GetAsyncEnumerator())
        {
            await items.MoveNextAsync();
            var pending = items.MoveNextAsync().AsTask();
            cancellation.Cancel();
            try { await pending; throw new Exception("Stream cancellation was lost."); }
            catch (OperationCanceledException) { }
        }
        Check(handler.Stream!.WasDisposed, "canceled stream disposed");
        using var bufferedHandler = new FakeHandler { Json = "{\"text\":\"first\"}\n{\"text\":\"second\"}\n" };
        using var bufferedHttp = new HttpClient(bufferedHandler);
        using var bufferedApi = new MizonApi(bufferedHttp, cache);
        using var bufferedCancellation = new CancellationTokenSource();
        await using var buffered = bufferedApi.SendStreamRequestAsync(new StreamOperation(), bufferedCancellation.Token).GetAsyncEnumerator();
        Check(await buffered.MoveNextAsync(), "buffered stream emits first row");
        bufferedCancellation.Cancel();
        try { await buffered.MoveNextAsync(); throw new Exception("Buffered rows ignored cancellation."); }
        catch (OperationCanceledException) { }
    }

    private static async Task CheckDependencyInjectionAsync()
    {
        var services = new ServiceCollection();
        services.AddMizonApi();
        services.AddMizonApi();
        services.AddHttpClient(nameof(MizonApi), client => client.DefaultRequestHeaders.Add("X-Diagnostics", "configured"))
            .ConfigurePrimaryHttpMessageHandler(() => new FakeHandler { RequireConfiguredHeader = true });
        await using (var provider = services.BuildServiceProvider())
        {
            var api = provider.GetRequiredService<MizonApi>();
            Check(ReferenceEquals(api, provider.GetRequiredService<MizonApi>()), "API singleton is stable");
            api.SetToken("configured");
            Check((await api.SendRequestAsync(new TestOperation())).IsSuccess, "DI uses configured named HttpClient");
            _ = provider.GetRequiredService<MizonSocket>();
        }
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var http = new HttpClient(new FakeHandler());
        new MizonApi(http, cache).Dispose();
        using var response = await http.GetAsync("https://sdk.test/");
        Check(response.IsSuccessStatusCode, "externally owned HttpClient remains usable");
        var owned = new HttpClient(new FakeHandler());
        new MizonApi(owned, cache, ownsHttpClient: true).Dispose();
        try { await owned.GetAsync("https://sdk.test/"); throw new Exception("Owned client survived disposal."); }
        catch (ObjectDisposedException) { }
    }

    internal static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class TestOperation : MizonApiRequest<TestRequest, TestResponse>
    {
        public TestOperation(bool post = false, bool cache = false, TimeSpan? ttl = null,
            CompressionMethod compression = CompressionMethod.GZip, TimeSpan? timeout = null, bool login = false, string query = "", bool authorized = true, bool fallbackQuery = false)
            : base(post ? ApiMethod.POST : ApiMethod.GET, "https://sdk.test/value", new TestRequest { Text = "آهنگ & test" })
        {
            RequestJsonTypeInfo = fallbackQuery ? PascalTransportJsonContext.Default.BaseApiRequestTestRequest : TransportJsonContext.Default.BaseApiRequestTestRequest;
            ResponseJsonTypeInfo = TransportJsonContext.Default.BaseApiResponseTestResponse;
            RequestQueryString = fallbackQuery ? null : query;
            NeedAuthorized = authorized;
            ClientCacheStrategy = cache ? ClientCacheStrategy.Enabled : ClientCacheStrategy.Disabled;
            MaximumClientCacheDuration = ttl;
            CompressionMethod = compression;
            CallTimeoutDuration = timeout ?? TimeSpan.FromSeconds(10);
            if (login) PropertyForToken = static response => response.Text!;
        }
    }

    private sealed class StreamOperation : MizonApiStreamRequest<TestRequest, TestResponse>
    {
        public StreamOperation() : base(ApiMethod.GET, "https://sdk.test/stream", new())
        {
            NeedAuthorized = false;
            RequestQueryString = "";
            ResponseJsonTypeInfo = TransportJsonContext.Default.TestResponse;
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Count;
        public string? Json;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool Wait;
        public bool RequireConfiguredHeader;
        public TaskCompletionSource? Gate;
        public string? LastBodyText;
        public Uri? LastUri;
        public readonly List<TrackedMemoryStream> ResponseStreams = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Count);
            LastUri = request.RequestUri;
            if (RequireConfiguredHeader) Check(request.Headers.Contains("X-Diagnostics"), "configured HTTP handler was bypassed");
            if (Wait) await Task.Delay(Timeout.Infinite, token);
            if (Gate is { } gate) await gate.Task.WaitAsync(token);
            if (request.Content is { } content)
            {
                await using var stream = await content.ReadAsStreamAsync(token);
                var encoding = content.Headers.ContentEncoding.FirstOrDefault();
                await using var decompressed = encoding switch
                {
                    "gzip" => (Stream)new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true),
                    "deflate" => new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true),
                    _ => new MemoryStream(await content.ReadAsByteArrayAsync(token))
                };
                using var json = await JsonDocument.ParseAsync(decompressed, cancellationToken: token);
                LastBodyText = json.RootElement.GetProperty("content").GetProperty("text").GetString();
            }
            var payload = Json ?? "{\"content\":{\"text\":\"" + (request.Headers.Authorization?.Parameter ?? "anonymous") + "\"}}";
            var responseStream = new TrackedMemoryStream(Encoding.UTF8.GetBytes(payload));
            ResponseStreams.Add(responseStream);
            return new HttpResponseMessage(Status) { Content = new StreamContent(responseStream) };
        }
    }

    private sealed class StreamHandler : HttpMessageHandler
    {
        public GatedStream? Stream;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Stream = new GatedStream();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(Stream) });
        }
    }

    private sealed class TrackedMemoryStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool WasDisposed;
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }

    private sealed class GatedStream : Stream
    {
        public readonly TaskCompletionSource Tail = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasDisposed;
        private int _step;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (_step == 0)
            {
                _step++;
                return WriteUtf8("\n{\"text\":\"first\"}\n", buffer);
            }
            if (_step == 1)
            {
                await Tail.Task.WaitAsync(token);
                _step++;
                return WriteUtf8("\n{\"text\":\"second\"}\n", buffer);
            }
            return 0;
        }
        private static int WriteUtf8(string value, Memory<byte> target) => Encoding.UTF8.GetBytes(value, target.Span);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { WasDisposed = true; Tail.TrySetCanceled(); base.Dispose(disposing); }
    }
}

internal sealed class TestRequest : IApiRequest { public string? Text { get; set; } }
internal sealed class TestResponse : IApiResponse { public string? Text { get; set; } }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<TestRequest>))]
[JsonSerializable(typeof(BaseApiResponse<TestResponse>))]
[JsonSerializable(typeof(TestResponse))]
internal partial class TransportJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(BaseApiRequest<TestRequest>))]
internal partial class PascalTransportJsonContext : JsonSerializerContext;
