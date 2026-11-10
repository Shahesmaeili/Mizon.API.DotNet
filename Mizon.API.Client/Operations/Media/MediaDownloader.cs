using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Mizon.API.Client.Operations.Media;

public sealed class MediaDownloader
{
    private static readonly char[] InvalidCacheNameChars = Path.GetInvalidFileNameChars();
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;

    public string CacheDirectory => _cacheRoot;

    public MediaDownloader(HttpClient httpClient, string cacheRoot)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        _cacheRoot = Path.GetFullPath(cacheRoot);
    }

    // Decide which key to use for caching. FileHash is content-based and better for dedup/resume across
    // different public ids. MediaPublicId is server-specific. We use FileHash when available, fall back to PublicId.
    private static string GetUniqueId(Responses.Media.MediaResponse info)
    {
        if (!string.IsNullOrWhiteSpace(info.FileHash))
            return info.FileHash;

        return info.MediaPublicId.ToString();
    }
    // Server-provided category should be used for folder placement (images/videos/music/others).
    // Returns the final cached file path (when fully downloaded)
    public string GetCachedFilePath(Responses.Media.MediaResponse info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var id = GetUniqueId(info);
        var folder = string.IsNullOrWhiteSpace(info.Category) ? "others" : info.Category;
        ValidatePathComponent(id, nameof(info.FileHash));
        ValidatePathComponent(folder, nameof(info.Category));
        ValidatePathComponent(info.FileName, nameof(info.FileName));
        var dir = Path.Combine(_cacheRoot, folder, id);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, info.FileName);
    }

    // Partial file path used during download
    private static string GetPartialFilePath(string finalPath) => finalPath + ".part";

    public bool IsCached(Responses.Media.MediaResponse info) => IsCompleteFile(GetCachedFilePath(info), info.FileSize);

    private static bool IsCompleteFile(string path, long expectedSize)
    {
        var file = new FileInfo(path);
        return file.Exists && (expectedSize <= 0 || file.Length == expectedSize);
    }

    private static void ValidatePathComponent(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or ".." || value.IndexOfAny(InvalidCacheNameChars) >= 0
            || value.Contains('/') || value.Contains('\\') || value.EndsWith('.') || value.EndsWith(' '))
            throw new ArgumentException("Media cache names must be single file or directory names.", parameterName);
    }

    /// <summary>
    /// Downloads a media file into the local cache and returns its final local path only after
    /// the download has completed and the partial file has been promoted to the final file.
    /// Use this for consumers such as image bindings that do not need chunk-level progress.
    /// </summary>
    public async ValueTask<string> DownloadToCacheAsync(
        Responses.Media.MediaResponse info,
        Uri storageUri,
        DownloadSession? session = null,
        IProgress<MediaDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? finalPath = null;
        await foreach (var chunk in DownloadCoreAsync(info, storageUri, session, includeData: false, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report(new(
                chunk.TotalDownloaded,
                chunk.FileSize,
                chunk.InstantaneousSpeedBytesPerSecond));
            if (chunk.IsLastChunk)
                finalPath = chunk.Path;
        }

        if (finalPath is null)
            throw new IOException($"Media download completed without a cached file. MediaPublicId='{info.MediaPublicId}'.");

        return finalPath;
    }

    public readonly record struct MediaDownloadProgress(
        long TotalDownloaded,
        long FileSize,
        double InstantaneousSpeedBytesPerSecond)
    {
        public double Percentage => FileSize > 0
            ? Math.Min(100d, (double)TotalDownloaded / FileSize * 100d)
            : 0d;
    }



    public sealed class DownloadChunk
    {
        public byte[] Data { get; }
        public int ChunkIndex { get; }
        public long TotalDownloaded { get; }
        public double InstantaneousSpeedBytesPerSecond { get; }
        public long FileSize { get; }
        // Local partial path, or the final cache path after the file has been promoted.
        public string? Path { get; }
        // Indicates this yielded chunk belongs to the last chunk of the file
        public bool IsLastChunk { get; }

        public DownloadChunk(byte[] data, int chunkIndex, long totalDownloaded, double speed, long fileSize, string? path, bool isLastChunk)
        {
            Data = data;
            ChunkIndex = chunkIndex;
            TotalDownloaded = totalDownloaded;
            InstantaneousSpeedBytesPerSecond = speed;
            FileSize = fileSize;
            Path = path;
            IsLastChunk = isLastChunk;
        }
    }

    // Session controller allows explicit Pause/Resume/Cancel without relying solely on external CancellationToken.
    public sealed class DownloadSession : IDisposable
    {
        private int _paused;
        private readonly object _cancellationGate = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly CancellationToken _cancellationToken;
        private bool _disposed;
        private bool _cancelling;

        public DownloadSession() => _cancellationToken = _cts.Token;

        public CancellationToken CancellationToken => _cancellationToken;

        public void Pause() => Interlocked.Exchange(ref _paused, 1);
        public void Resume() => Interlocked.Exchange(ref _paused, 0);
        public bool IsPaused => Volatile.Read(ref _paused) != 0;
        public void Cancel() => CancelCore(dispose: false);
        public void Dispose() => CancelCore(dispose: true);

        private void CancelCore(bool dispose)
        {
            lock (_cancellationGate)
            {
                if (_disposed) return;
                _disposed = dispose;
                // The active cancellation completes disposal, including disposal requested by a callback.
                if (_cancelling) return;
                _cancelling = true;
            }

            try
            {
                // Cancellation callbacks must run outside the lock; they can reenter this session.
                _cts.Cancel();
            }
            finally
            {
                lock (_cancellationGate)
                {
                    _cancelling = false;
                    if (_disposed) _cts.Dispose();
                }
            }
        }

        internal async Task WaitWhilePausedAsync(CancellationToken ct)
        {
            while (IsPaused)
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }
    }

    // Download stream as IAsyncEnumerable of DownloadChunk. Each yielded item contains the bytes written for that chunk
    // plus metadata for progress and instantaneous speed.
    public IAsyncEnumerable<DownloadChunk> DownloadAsync(Responses.Media.MediaResponse info, Uri storageUri, DownloadSession? session = null, CancellationToken cancellationToken = default)
        => DownloadCoreAsync(info, storageUri, session, includeData: true, cancellationToken);

    private async IAsyncEnumerable<DownloadChunk> DownloadCoreAsync(Responses.Media.MediaResponse info, Uri storageUri, DownloadSession? session, bool includeData, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(storageUri);
        if (!storageUri.IsAbsoluteUri || (!storageUri.IsFile && storageUri.Scheme is not ("http" or "https")))
            throw new ArgumentException("Media storage must be an absolute file, HTTP or HTTPS URI.", nameof(storageUri));

        var finalPath = GetCachedFilePath(info);
        var partialPath = GetPartialFilePath(finalPath);
        // The app and its out-of-process Search provider share this cache. One
        // writer owns a partial file; other callers wait, then reuse the result.
        using var cts = session is null ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.CancellationToken);
        var token = cts?.Token ?? cancellationToken;
        await using var cacheLease = await AcquireCacheLeaseAsync(finalPath, token).ConfigureAwait(false);

        // A cache hit still returns the final path to progressive consumers.
        if (IsCompleteFile(finalPath, info.FileSize))
        {
            // File already fully downloaded and present in cache.
            // Yield a final DownloadChunk so callers receive the local path immediately.
            var fi = new FileInfo(finalPath);
            var existingTotal = fi.Length;
            yield return new DownloadChunk(Array.Empty<byte>(), 0, existingTotal, 0, existingTotal, finalPath, true);
            yield break;
        }


        var chunkSize = info.ChunkSize > 0 ? info.ChunkSize : 81920;
        long? fileSize = info.FileSize > 0 ? info.FileSize : null;
        // Tiny profile images do not need an 80 KiB buffer in both FileStreams.
        var fileBufferSize = fileSize.HasValue ? (int)Math.Clamp(fileSize.Value, 4096L, 81920L) : 81920;
        await using var localSource = storageUri.IsFile
            ? new FileStream(storageUri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, fileBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        if (localSource is not null) fileSize = localSource.Length;
        if (fileSize.HasValue) fileBufferSize = (int)Math.Clamp(fileSize.Value, 4096L, 81920L);
        await using var file = new FileStream(partialPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, fileBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        // Only complete chunks are trusted when resuming after cancellation or a process exit.
        var downloaded = file.Length;
        if (fileSize.HasValue && downloaded > fileSize.Value) downloaded = 0;
        if (downloaded != fileSize) downloaded -= downloaded % chunkSize;
        file.SetLength(downloaded);
        file.Position = downloaded;
        localSource?.Seek(downloaded, SeekOrigin.Begin);

        var chunkIndex = checked((int)(downloaded / chunkSize));
        var scratch = ArrayPool<byte>.Shared.Rent(Math.Min(chunkSize, 81920));
        HttpSource? remoteSource = null;
        try
        {
            while (!fileSize.HasValue || downloaded < fileSize.Value)
            {
                token.ThrowIfCancellationRequested();
                if (session is not null) await session.WaitWhilePausedAsync(token).ConfigureAwait(false);
                var attempt = 0;
                byte[] data;
                int count;
                double speed;
                bool finished;
                while (true)
                {
                    // ResponseHeadersRead only applies HttpClient.Timeout to headers. Bound body reads too.
                    using var timeout = !storageUri.IsFile && _httpClient.Timeout != Timeout.InfiniteTimeSpan
                        ? CancellationTokenSource.CreateLinkedTokenSource(token) : null;
                    timeout?.CancelAfter(_httpClient.Timeout);
                    var readToken = timeout?.Token ?? token;
                    var started = Stopwatch.GetTimestamp();
                    try
                    {
                        var expected = (int)Math.Min(chunkSize, fileSize.HasValue ? fileSize.Value - downloaded : chunkSize);
                        Stream source;
                        if (localSource is not null)
                            source = localSource;
                        else
                        {
                            remoteSource ??= await OpenHttpSourceAsync(storageUri, downloaded, expected, fileSize, scratch, readToken).ConfigureAwait(false);
                            fileSize ??= remoteSource.FileSize;
                            expected = (int)Math.Min(expected, fileSize.HasValue ? fileSize.Value - downloaded : expected);
                            source = remoteSource.Stream;
                        }

                        data = includeData ? new byte[expected] : Array.Empty<byte>();
                        count = 0;
                        while (count < expected)
                        {
                            var buffer = includeData ? data.AsMemory(count, Math.Min(expected - count, scratch.Length))
                                : scratch.AsMemory(0, Math.Min(expected - count, scratch.Length));
                            var read = await source.ReadAsync(buffer, readToken).ConfigureAwait(false);
                            if (read == 0) break;
                            await file.WriteAsync(buffer[..read], readToken).ConfigureAwait(false);
                            count += read;
                        }
                        if (fileSize.HasValue && count != expected)
                            throw new EndOfStreamException($"Media response ended at byte {downloaded + count}; expected {fileSize.Value} bytes.");
                        finished = fileSize.HasValue ? downloaded + count == fileSize.Value : count < expected;
                        if (finished && remoteSource is { Continuous: true, FileSize: null } && fileSize.HasValue
                            && await source.ReadAsync(scratch.AsMemory(0, 1), readToken).ConfigureAwait(false) != 0)
                            throw new InvalidDataException("The media response exceeds its metadata length.");
                        if (finished && !fileSize.HasValue) fileSize = downloaded + count;
                        if (includeData && count != data.Length) Array.Resize(ref data, count);
                        await file.FlushAsync(readToken).ConfigureAwait(false);
                        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                        speed = seconds > 0 ? count / seconds : 0;
                        if (remoteSource is { Continuous: false })
                        {
                            await remoteSource.DisposeAsync().ConfigureAwait(false);
                            remoteSource = null;
                        }
                        break;
                    }
                    catch (Exception error)
                    {
                        // A failed chunk must never leave an unverified tail that a later resume trusts.
                        file.SetLength(downloaded);
                        file.Position = downloaded;
                        if (remoteSource is not null)
                        {
                            await remoteSource.DisposeAsync().ConfigureAwait(false);
                            remoteSource = null;
                        }
                        token.ThrowIfCancellationRequested();
                        if (error is OperationCanceledException && timeout?.IsCancellationRequested == true)
                            throw new TimeoutException("The media response body did not complete within HttpClient.Timeout.", error);
                        if (storageUri.IsFile || error is InvalidDataException || error is not (IOException or HttpRequestException) || ++attempt >= 6)
                            throw;
                        await Task.Delay(200 * (1 << (attempt - 1)), token).ConfigureAwait(false);
                    }
                }

                downloaded += count;
                if (finished)
                {
                    await file.DisposeAsync().ConfigureAwait(false);
                    File.Move(partialPath, finalPath, overwrite: true);
                }
                yield return new DownloadChunk(data, chunkIndex++, downloaded, speed, fileSize ?? 0, finished ? finalPath : partialPath, finished);
                if (finished) yield break;
            }

            // Empty local files and a complete partial file also need a final notification.
            token.ThrowIfCancellationRequested();
            await file.DisposeAsync().ConfigureAwait(false);
            File.Move(partialPath, finalPath, overwrite: true);
            yield return new DownloadChunk(Array.Empty<byte>(), chunkIndex, downloaded, 0, downloaded, finalPath, true);
        }
        finally
        {
            if (remoteSource is not null) await remoteSource.DisposeAsync().ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    private async ValueTask<HttpSource> OpenHttpSourceAsync(Uri uri, long start, int count, long? fileSize, byte[] scratch, CancellationToken token)
    {
        using var request = new HttpRequestMessage(global::System.Net.Http.HttpMethod.Get, uri);
        if (fileSize.HasValue)
            request.Headers.Range = new RangeHeaderValue(start, checked(start + count - 1L));
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            var continuous = response.StatusCode == HttpStatusCode.OK;
            long? responseSize;
            if (continuous)
                responseSize = response.Content.Headers.ContentLength;
            else
            {
                var range = response.Content.Headers.ContentRange;
                if (response.StatusCode != HttpStatusCode.PartialContent || range is null || !range.HasRange
                    || range.Unit != "bytes" || range.From != start || range.To != Math.Min(start + count - 1L, (range.Length ?? long.MaxValue) - 1))
                    throw new InvalidDataException("The media server returned an unexpected Content-Range.");
                responseSize = range.Length;
            }
            if (fileSize.HasValue && responseSize.HasValue && fileSize != responseSize)
                throw new InvalidDataException("The media response length does not match its metadata.");
            var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            // Servers may ignore Range. Consume one continuous body, skipping the existing prefix once.
            if (continuous)
            {
                var remaining = start;
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(scratch.AsMemory(0, (int)Math.Min(scratch.Length, remaining)), token).ConfigureAwait(false);
                    if (read == 0) throw new EndOfStreamException("The media response is shorter than the cached prefix.");
                    remaining -= read;
                }
            }
            return new HttpSource(response, stream, responseSize, continuous);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private sealed class HttpSource(HttpResponseMessage response, Stream stream, long? fileSize, bool continuous) : IAsyncDisposable
    {
        internal Stream Stream { get; } = stream;
        internal long? FileSize { get; } = fileSize;
        internal bool Continuous { get; } = continuous;

        public ValueTask DisposeAsync()
        {
            response.Dispose(); // HttpContent owns and disposes the response stream.
            return ValueTask.CompletedTask;
        }
    }

    private static async ValueTask<FileStream> AcquireCacheLeaseAsync(string finalPath, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(finalPath + ".download-lock", FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
