using System.Diagnostics;
using System.Net;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Mizon.API;
using Mizon.API.Client.Operations.Search;
using Mizon.API.Client.Requests.Search;
using Mizon.API.Client.Responses.Search;

if (JsonSerializer.IsReflectionEnabledByDefault)
    throw new InvalidOperationException("The benchmark requires generated JSON metadata.");

if (args.Length > 1) throw new ArgumentException("Use --verify or one JSON report path.");
var verifyOnly = args is ["--verify"];
var outputPath = args.Length > 0 && !verifyOnly ? Path.GetFullPath(args[0]) : null;
var large = CreateFixture(1_200);
var small = CreateFixture(1);
var largePayload = JsonSerializer.SerializeToUtf8Bytes(new BaseApiResponse<BenchmarkResponse> { Content = large }, BenchmarkJsonContext.Default.BaseApiResponseBenchmarkResponse);
var smallPayload = JsonSerializer.SerializeToUtf8Bytes(new BaseApiResponse<BenchmarkResponse> { Content = small }, BenchmarkJsonContext.Default.BaseApiResponseBenchmarkResponse);
var searchRows = Enumerable.Range(0, 200).Select(i => new GlobalSearchItemResponse
{
    ItemType = i % 2 == 0 ? "music" : "client",
    PublicId = new Guid(i, 42, 57, 12, 23, 34, 45, 56, 67, 78, 89),
    Title = i % 2 == 0 ? $"Music {i:D4}" : null,
    PersianTitle = i % 2 == 0 ? $"آهنگ آزمایشی {i:D4}" : null,
    Username = i % 2 != 0 ? $"user_{i:D4}" : null,
    FirstName = i % 2 != 0 ? "نام" : null,
    LastName = i % 2 != 0 ? $"کاربر {i:D4}" : null,
    CoverMediaPublicId = i % 2 == 0 ? Guid.Parse("0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a72") : null,
    ProfileMediaPublicId = i % 2 != 0 ? Guid.Parse("0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a72") : null
}).ToArray();
byte[] searchPayload;
using (var buffer = new MemoryStream())
{
    foreach (var row in searchRows)
    {
        JsonSerializer.Serialize(buffer, row, BenchmarkJsonContext.Default.GlobalSearchItemResponse);
        buffer.WriteByte((byte)'\n');
    }
    searchPayload = buffer.ToArray();
}

using var handler = new BenchmarkHandler(smallPayload, largePayload, searchPayload);
using var http = new HttpClient(handler);
using var cache = new MemoryCache(new MemoryCacheOptions());
using var api = new MizonApi(http, cache);
api.SetToken("synthetic-benchmark-token");
var smallOperation = new BenchmarkOperation("small", cached: false);
var largeOperation = new BenchmarkOperation("large", cached: false);
var cachedOperation = new BenchmarkOperation("small", cached: true);
var searchOperation = new GlobalSearchOperation(new GlobalSearchRequest { Query = "آهنگ" });

// Correctness is checked before timing, and a checksum plus request count is checked per batch.
VerifyResponse(await api.SendRequestAsync(smallOperation), small);
VerifyResponse(await api.SendRequestAsync(largeOperation), large);
VerifyResponse(await api.SendRequestAsync(cachedOperation), small);
await VerifySearchAsync();
if (verifyOnly)
{
    var cached = await api.SendRequestAsync(cachedOperation);
    if (!cached.IsFromCache) throw new InvalidDataException("Cache hit validation failed.");
    Console.WriteLine($"SDK benchmark fixtures verified on {RuntimeInformation.FrameworkDescription}; no timing run.");
    return;
}
var measurements = new List<BenchmarkMeasurement>();
foreach (var scenario in new[]
{
    new BenchmarkScenario("cached-get", 50_000, 20_000, 1, cached: true, streaming: false),
    new BenchmarkScenario("small-json-get", 5_000, 2_000, 1, cached: false, streaming: false),
    new BenchmarkScenario("large-json-1200-rows", 100, 100, 1_200, cached: false, streaming: false),
    new BenchmarkScenario("ndjson-search-200-rows", 150, 100, 200, cached: false, streaming: true)
})
{
    await RunBatchAsync(scenario, scenario.Warmup);
    var samples = new List<BenchmarkSample>();
    for (var index = 0; index < 9; index++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var callsBefore = handler.Count;
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        var checksum = await RunBatchAsync(scenario, scenario.Iterations);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        if (checksum != (long)scenario.Iterations * scenario.Rows
            || handler.Count - callsBefore != (scenario.Cached ? 0 : scenario.Iterations))
            throw new InvalidDataException("Benchmark checksum or transport/cache count failed.");
        samples.Add(new BenchmarkSample
        {
            MicrosecondsPerOperation = elapsed.TotalMicroseconds / scenario.Iterations,
            AllocatedBytesPerOperation = (double)allocated / scenario.Iterations
        });
    }
    measurements.Add(new BenchmarkMeasurement
    {
        Name = scenario.Name,
        IterationsPerSample = scenario.Iterations,
        Samples = samples,
        MedianMicrosecondsPerOperation = samples.Select(s => s.MicrosecondsPerOperation).Order().ElementAt(samples.Count / 2),
        MedianAllocatedBytesPerOperation = samples.Select(s => s.AllocatedBytesPerOperation).Order().ElementAt(samples.Count / 2)
    });
}

using var process = Process.GetCurrentProcess();
var report = new SdkBenchmarkReport
{
    Runtime = RuntimeInformation.FrameworkDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    OperatingSystem = RuntimeInformation.OSDescription,
    TimestampUtc = DateTimeOffset.UtcNow,
    IsServerGC = GCSettings.IsServerGC,
    DynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported,
    TieredCompilationEnvironment = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    CacheAssemblyVersion = typeof(MemoryCache).Assembly.GetName().Version?.ToString(),
    LargePayloadBytes = largePayload.Length,
    SearchPayloadBytes = searchPayload.Length,
    LargePayloadSha256 = Convert.ToHexString(SHA256.HashData(largePayload)),
    SearchPayloadSha256 = Convert.ToHexString(SHA256.HashData(searchPayload)),
    ProcessPeakWorkingSetBytes = process.PeakWorkingSet64,
    Measurements = measurements
};
var json = JsonSerializer.Serialize(report, BenchmarkJsonContext.Default.SdkBenchmarkReport);
if (outputPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, json);
}
Console.WriteLine(json);

async ValueTask<long> RunBatchAsync(BenchmarkScenario scenario, int iterations)
{
    long checksum = 0;
    for (var index = 0; index < iterations; index++)
    {
        if (scenario.Streaming)
        {
            await foreach (var item in api.SendStreamRequestAsync(searchOperation))
            {
                if (item.PublicId == Guid.Empty) throw new InvalidDataException("Search result lost its public ID.");
                checksum++;
            }
        }
        else
        {
            var operation = scenario.Cached ? cachedOperation : scenario.Rows == 1 ? smallOperation : largeOperation;
            var response = await api.SendRequestAsync(operation);
            if (!response.IsSuccess || response.Content is null || response.IsFromCache != scenario.Cached)
                throw new InvalidDataException("Unexpected SDK response or cache status.");
            checksum += response.Content.Rows.Count;
        }
    }
    return checksum;
}

async Task VerifySearchAsync()
{
    var index = 0;
    await foreach (var item in api.SendStreamRequestAsync(searchOperation))
    {
        if (index >= searchRows.Length || JsonSerializer.Serialize(item, BenchmarkJsonContext.Default.GlobalSearchItemResponse)
            != JsonSerializer.Serialize(searchRows[index++], BenchmarkJsonContext.Default.GlobalSearchItemResponse))
            throw new InvalidDataException("NDJSON content round-trip failed.");
    }
    if (index != searchRows.Length) throw new InvalidDataException("NDJSON row count failed.");
}

static void VerifyResponse(BaseApiResponse<BenchmarkResponse> actual, BenchmarkResponse expected)
{
    if (!actual.IsSuccess || actual.Content is null
        || JsonSerializer.Serialize(actual.Content, BenchmarkJsonContext.Default.BenchmarkResponse)
        != JsonSerializer.Serialize(expected, BenchmarkJsonContext.Default.BenchmarkResponse))
        throw new InvalidDataException("Response content round-trip failed.");
}

static BenchmarkResponse CreateFixture(int count) => new()
{
    Rows = Enumerable.Range(0, count).Select(i => new BenchmarkRow
    {
        PublicId = new Guid(i, 42, 57, 12, 23, 34, 45, 56, 67, 78, 89),
        Title = $"آهنگ آزمایشی شماره {i:D4}",
        Artist = $"Artist {i % 80:D2}",
        CoverMediaPublicId = Guid.Parse("0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a72"),
        Duration = TimeSpan.FromSeconds(180 + i % 100),
        LikesCount = 3_000 + i,
        ViewedByMe = i % 2 == 0,
        Waveform = [12, 80, 42, 91, 27, 65, 10, 97]
    }).ToList()
};

internal sealed record BenchmarkScenario(string Name, int Iterations, int Warmup, int Rows, bool cached, bool streaming)
{
    public bool Cached => cached;
    public bool Streaming => streaming;
}

internal sealed class BenchmarkOperation : MizonApiRequest<BenchmarkRequest, BenchmarkResponse>
{
    public BenchmarkOperation(string fixture, bool cached) : base(Mizon.API.HttpMethod.GET, "https://benchmark.invalid/" + fixture, new())
    {
        RequestJsonTypeInfo = BenchmarkJsonContext.Default.BaseApiRequestBenchmarkRequest;
        ResponseJsonTypeInfo = BenchmarkJsonContext.Default.BaseApiResponseBenchmarkResponse;
        RequestQueryString = "?Fixture=1";
        ClientCacheStrategy = cached ? ClientCacheStrategy.Enabled : ClientCacheStrategy.Disabled;
        MaximumClientCacheDuration = TimeSpan.FromHours(1);
    }
}

internal sealed class BenchmarkRequest : IApiRequest;
internal sealed class BenchmarkResponse : IApiResponse
{
    public List<BenchmarkRow> Rows { get; set; } = [];
}

internal sealed class BenchmarkRow
{
    public Guid PublicId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public Guid CoverMediaPublicId { get; set; }
    public TimeSpan Duration { get; set; }
    public int LikesCount { get; set; }
    public bool ViewedByMe { get; set; }
    public short[] Waveform { get; set; } = [];
}

internal sealed class BenchmarkHandler(byte[] small, byte[] large, byte[] search) : HttpMessageHandler
{
    public long Count { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = request.RequestUri?.AbsolutePath switch
        {
            "/small" => small,
            "/large" => large,
            "/Search/Global" => search,
            _ => throw new InvalidOperationException("Unexpected benchmark endpoint; no real network is allowed.")
        };
        Count++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
    }
}

internal sealed class SdkBenchmarkReport
{
    public string Runtime { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public bool IsServerGC { get; set; }
    public bool DynamicCodeSupported { get; set; }
    public string? TieredCompilationEnvironment { get; set; }
    public string? CacheAssemblyVersion { get; set; }
    public int LargePayloadBytes { get; set; }
    public int SearchPayloadBytes { get; set; }
    public string LargePayloadSha256 { get; set; } = string.Empty;
    public string SearchPayloadSha256 { get; set; } = string.Empty;
    public long ProcessPeakWorkingSetBytes { get; set; }
    public List<BenchmarkMeasurement> Measurements { get; set; } = [];
}

internal sealed class BenchmarkMeasurement
{
    public string Name { get; set; } = string.Empty;
    public int IterationsPerSample { get; set; }
    public double MedianMicrosecondsPerOperation { get; set; }
    public double MedianAllocatedBytesPerOperation { get; set; }
    public List<BenchmarkSample> Samples { get; set; } = [];
}

internal sealed class BenchmarkSample
{
    public double MicrosecondsPerOperation { get; set; }
    public double AllocatedBytesPerOperation { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BaseApiRequest<BenchmarkRequest>))]
[JsonSerializable(typeof(BaseApiResponse<BenchmarkResponse>))]
[JsonSerializable(typeof(BenchmarkResponse))]
[JsonSerializable(typeof(GlobalSearchItemResponse))]
[JsonSerializable(typeof(SdkBenchmarkReport))]
internal partial class BenchmarkJsonContext : JsonSerializerContext;
