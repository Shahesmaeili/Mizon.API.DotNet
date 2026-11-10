using System.Net;
using System.Net.Http.Headers;
using Mizon.API.Client.Operations.Media;
using Mizon.API.Client.Responses.Media;

internal static class MediaChecks
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("MIZON_DIAGNOSTICS_ROOT") ?? @"D:\MizonTaskDiagnostics\SdkOptimization", "Media", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var data = new byte[10007];
        new Random(24).NextBytes(data);
        var source = Path.Combine(root, "source.bin");
        await File.WriteAllBytesAsync(source, data);
        var sourceUri = new Uri(source);
        var remoteUri = new Uri("https://media.invalid/source.bin");
        using var handler = new MediaHandler(data);
        using var http = new HttpClient(handler);
        var id = 0;
        MediaDownloader Downloader(HttpClient? client = null) => new(client ?? http, Path.Combine(root, (++id).ToString()));

        var info = Info(data.Length);
        var local = Downloader();
        var received = new List<byte>();
        var previous = 0L;
        var chunks = 0;
        await foreach (var chunk in local.DownloadAsync(info, sourceUri))
        {
            Check(File.Exists(chunk.Path), "Every published chunk path already exists, including the final path");
            Check(chunk.TotalDownloaded >= previous && chunk.TotalDownloaded <= data.Length, "Chunk progress is monotonic and bounded");
            previous = chunk.TotalDownloaded;
            received.AddRange(chunk.Data);
            chunks++;
        }
        Check(received.SequenceEqual(data) && chunks == 10, "Local progressive chunks retain exact bytes and boundaries");

        var resumed = Downloader();
        var resumedPath = resumed.GetCachedFilePath(info);
        await File.WriteAllBytesAsync(resumedPath + ".part", data[..1500]);
        var progress = new List<MediaDownloader.MediaDownloadProgress>();
        await resumed.DownloadToCacheAsync(info, sourceUri, progress: new InlineProgress(progress));
        Check((await File.ReadAllBytesAsync(resumedPath)).SequenceEqual(data), "Unaligned partial file resumes without gaps or overlap");
        Check(progress.All(p => p.TotalDownloaded <= data.Length) && progress[^1].TotalDownloaded == data.Length, "Resumed progress never double counts the incomplete prefix");

        var completePartial = Downloader();
        await File.WriteAllBytesAsync(completePartial.GetCachedFilePath(info) + ".part", data);
        Check(File.Exists(await completePartial.DownloadToCacheAsync(info, remoteUri)), "A complete partial file is promoted without HTTP");
        Check(handler.Requests == 0, "Complete partial promotion does not request media again");

        var emptySource = Path.Combine(root, "empty.bin");
        await File.WriteAllBytesAsync(emptySource, []);
        Check(new FileInfo(await Downloader().DownloadToCacheAsync(Info(0), new Uri(emptySource))).Length == 0, "Empty local files finish and enter the cache");

        var ranged = Downloader();
        var pair = await Task.WhenAll(ranged.DownloadToCacheAsync(info, remoteUri).AsTask(),
            new MediaDownloader(http, ranged.CacheDirectory).DownloadToCacheAsync(info, remoteUri).AsTask());
        Check(pair[0] == pair[1] && (await File.ReadAllBytesAsync(pair[0])).SequenceEqual(data), "Concurrent downloader instances share a correct cached file");
        Check(handler.Requests == 10 && handler.Disposals == 10, "One request per range and every response stream disposed");
        var before = handler.Requests;
        await ranged.DownloadToCacheAsync(info, remoteUri);
        Check(handler.Requests == before, "Cache hit performs no HTTP request");
        await File.WriteAllBytesAsync(pair[0], [1]);
        Check(!ranged.IsCached(info), "A truncated final file is not reported as cached");
        await ranged.DownloadToCacheAsync(info, remoteUri);
        Check((await File.ReadAllBytesAsync(pair[0])).SequenceEqual(data), "A truncated final file is replaced only after a complete download");

        using var noRangeHandler = new MediaHandler(data) { IgnoreRange = true };
        using var noRangeClient = new HttpClient(noRangeHandler);
        var noRange = Downloader(noRangeClient);
        await File.WriteAllBytesAsync(noRange.GetCachedFilePath(info) + ".part", data[..2048]);
        Check((await File.ReadAllBytesAsync(await noRange.DownloadToCacheAsync(info, remoteUri))).SequenceEqual(data), "HTTP 200 Range fallback resumes from the correct source offset");
        Check(noRangeHandler.Requests == 1 && noRangeHandler.Disposals == 1, "A server ignoring Range is consumed as one stream");

        using var unknownHandler = new MediaHandler(data) { IgnoreRange = true, UnknownLength = true };
        using var unknownClient = new HttpClient(unknownHandler);
        var unknownPath = await Downloader(unknownClient).DownloadToCacheAsync(Info(0), remoteUri);
        Check((await File.ReadAllBytesAsync(unknownPath)).SequenceEqual(data), "Unknown length HTTP media reads until EOF and completes");

        using var shortHandler = new MediaHandler(data) { TruncateFirstResponse = true };
        using var shortClient = new HttpClient(shortHandler);
        var shortPath = await Downloader(shortClient).DownloadToCacheAsync(info, remoteUri);
        Check((await File.ReadAllBytesAsync(shortPath)).SequenceEqual(data) && shortHandler.Requests == 11, "A short response retries the same chunk without publishing corrupt bytes");

        using var invalidHandler = new MediaHandler(data) { InvalidRange = true };
        using var invalidClient = new HttpClient(invalidHandler);
        var invalid = Downloader(invalidClient);
        await ThrowsAsync<InvalidDataException>(() => invalid.DownloadToCacheAsync(info, remoteUri).AsTask(), "Invalid Content-Range fails safely");
        Check(!File.Exists(invalid.GetCachedFilePath(info)) && invalidHandler.Disposals == 1, "Invalid headers never create a final file and response is disposed");

        using var stalledHandler = new MediaHandler(data) { StallBody = true };
        using var stalledClient = new HttpClient(stalledHandler) { Timeout = TimeSpan.FromMilliseconds(80) };
        var stalled = Downloader(stalledClient);
        await ThrowsAsync<TimeoutException>(() => stalled.DownloadToCacheAsync(info, remoteUri).AsTask(), "HttpClient timeout also bounds response body reads");
        Check(stalledHandler.Disposals == 1 && !File.Exists(stalled.GetCachedFilePath(info)), "Timed out body is disposed without a final cache file");

        using var cancelledHandler = new MediaHandler(data) { StallBody = true };
        using var cancelledClient = new HttpClient(cancelledHandler) { Timeout = Timeout.InfiniteTimeSpan };
        using var cancellation = new CancellationTokenSource(80);
        await ThrowsAsync<OperationCanceledException>(() => Downloader(cancelledClient).DownloadToCacheAsync(info, remoteUri, cancellationToken: cancellation.Token).AsTask(), "Caller cancellation interrupts a body read");
        Check(cancelledHandler.Disposals == 1, "Cancelled body stream is disposed");

        var waiting = Downloader();
        var lockPath = waiting.GetCachedFilePath(info) + ".download-lock";
        using (var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            using var session = new MediaDownloader.DownloadSession();
            var pending = waiting.DownloadToCacheAsync(info, remoteUri, session).AsTask();
            session.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => pending, "Session cancellation interrupts cross-process cache lock waiting");
        }
        using (var paused = new MediaDownloader.DownloadSession())
        {
            paused.Pause();
            var pending = Downloader().DownloadToCacheAsync(info, sourceUri, paused).AsTask();
            Check(!pending.IsCompleted, "Paused download waits before reading media");
            paused.Resume();
            Check(File.Exists(await pending.WaitAsync(TimeSpan.FromSeconds(3))), "Session resumes and finishes");
        }
        var disposedDownloader = Downloader();
        var disposedSession = new MediaDownloader.DownloadSession();
        disposedSession.Pause();
        var disposedToken = disposedSession.CancellationToken;
        var disposedDownload = disposedDownloader.DownloadToCacheAsync(info, sourceUri, disposedSession).AsTask();
        Check(!disposedDownload.IsCompleted, "Paused session holds an active download before disposal");
        disposedSession.Dispose();
        await ThrowsAsync<OperationCanceledException>(() => disposedDownload.WaitAsync(TimeSpan.FromSeconds(3)), "Disposing a paused session cancels its active download");
        Check(!File.Exists(disposedDownloader.GetCachedFilePath(info) + ".download-lock"), "Session disposal releases the active cache lease");
        Check(disposedSession.CancellationToken == disposedToken && disposedToken.IsCancellationRequested, "Disposed session safely exposes its cancelled token");
        disposedSession.Dispose();
        disposedSession.Cancel();
        Check(true, "Repeated disposal and cancellation are harmless");
        for (var i = 0; i < 100; i++)
        {
            using var concurrentSession = new MediaDownloader.DownloadSession();
            using var registration = concurrentSession.CancellationToken.Register(concurrentSession.Dispose);
            await Task.WhenAll(Task.Run(concurrentSession.Cancel), Task.Run(concurrentSession.Dispose), Task.Run(concurrentSession.Cancel));
            if (!concurrentSession.CancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("Concurrent session disposal did not cancel its token.");
        }
        Check(true, "100 concurrent cancel/dispose and reentrant disposal races finish safely");
        var interrupted = Downloader();
        await using (var iterator = interrupted.DownloadAsync(info, sourceUri).GetAsyncEnumerator())
            Check(await iterator.MoveNextAsync(), "Progressive iterator produces an initial chunk");
        Check(!File.Exists(interrupted.GetCachedFilePath(info) + ".download-lock"), "Early iterator disposal releases the cache lease");
        Check((await File.ReadAllBytesAsync(await interrupted.DownloadToCacheAsync(info, sourceUri))).SequenceEqual(data), "An abandoned iterator resumes correctly");

        foreach (var invalidName in new[] { "../outside.bin", @"..\outside.bin", @"C:\outside.bin", "file.bin:stream", "file.bin." })
        {
            var bad = Info(data.Length);
            bad.FileName = invalidName;
            await ThrowsAsync<ArgumentException>(() => Downloader().DownloadToCacheAsync(bad, sourceUri).AsTask(), "Cache rejects path traversal/alternate stream names");
        }

        var smallSource = Path.Combine(root, "small-image.bin");
        await File.WriteAllBytesAsync(smallSource, data[..2048]);
        var smallBefore = GC.GetTotalAllocatedBytes(precise: true);
        var smallPath = await Downloader().DownloadToCacheAsync(Info(2048), new Uri(smallSource));
        var smallAllocation = GC.GetTotalAllocatedBytes(precise: true) - smallBefore;
        Check((await File.ReadAllBytesAsync(smallPath)).SequenceEqual(data[..2048]), "Small-image buffering preserves exact image bytes");
        Console.WriteLine($"Media completion helper allocation for 2 KiB image: {smallAllocation:N0} bytes.");

        // This test needs no database, real media files, app process or external HTTP server.
        var large = new byte[8 * 1024 * 1024];
        var largeSource = Path.Combine(root, "large.bin");
        await File.WriteAllBytesAsync(largeSource, large);
        var largeInfo = Info(large.Length);
        largeInfo.ChunkSize = large.Length; // Existing database seeds may describe one whole-media chunk.
        var beforeAllocated = GC.GetTotalAllocatedBytes(precise: true);
        await Downloader().DownloadToCacheAsync(largeInfo, new Uri(largeSource));
        var completeAllocation = GC.GetTotalAllocatedBytes(precise: true) - beforeAllocated;
        Check(completeAllocation < 1024 * 1024, "Complete-file download uses bounded memory instead of a whole-media byte array");
        Console.WriteLine($"Media completion helper allocation for 8 MiB/one chunk: {completeAllocation:N0} bytes.");
        Console.WriteLine($"Media diagnostics artifacts: {root}");
    }

    private static MediaResponse Info(int size) => new()
    {
        MediaPublicId = Guid.Parse("ae1cbe45-ec4a-499f-a959-523306daed81"), FileName = "audio.bin", Category = "test",
        FileHash = "fixture", ChunkSize = 1024, FileSize = size, StoragePath = "https://media.invalid/source.bin"
    };

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Console.WriteLine("PASS media: " + message);
    }

    private static async Task ThrowsAsync<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); }
        catch (T) { Check(true, message); return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + message);
    }

    private sealed class InlineProgress(List<MediaDownloader.MediaDownloadProgress> values) : IProgress<MediaDownloader.MediaDownloadProgress>
    {
        public void Report(MediaDownloader.MediaDownloadProgress value) => values.Add(value);
    }

    private sealed class MediaHandler(byte[] data) : HttpMessageHandler
    {
        public bool IgnoreRange { get; init; }
        public bool UnknownLength { get; init; }
        public bool TruncateFirstResponse { get; init; }
        public bool InvalidRange { get; init; }
        public bool StallBody { get; init; }
        public int Requests;
        public int Disposals;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref Requests);
            var range = IgnoreRange ? null : request.Headers.Range?.Ranges.Single();
            var start = (int)(range?.From ?? 0);
            var end = (int)(range?.To ?? data.Length - 1);
            var length = end - start + 1;
            if (TruncateFirstResponse && number == 1) length /= 2;
            var stream = new TestStream(data.AsMemory(start, length), () => Interlocked.Increment(ref Disposals), StallBody);
            var content = new StreamContent(stream);
            if (!UnknownLength) content.Headers.ContentLength = end - start + 1;
            if (range is not null)
                content.Headers.ContentRange = new ContentRangeHeaderValue(start + (InvalidRange ? 1 : 0), end, data.Length);
            return Task.FromResult(new HttpResponseMessage(range is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent) { Content = content });
        }
    }

    private sealed class TestStream(ReadOnlyMemory<byte> data, Action disposed, bool stall) : Stream
    {
        private int _position;
        private bool _disposed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (stall) await Task.Delay(Timeout.Infinite, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(Math.Min(137, buffer.Length), data.Length - _position);
            data.Slice(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
        protected override void Dispose(bool disposing)
        {
            if (!_disposed) { _disposed = true; disposed(); }
            base.Dispose(disposing);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
