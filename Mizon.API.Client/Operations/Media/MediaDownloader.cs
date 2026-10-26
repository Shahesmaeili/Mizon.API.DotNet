using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Mizon.API.Client.Operations.Media;

public sealed class MediaDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;

    public MediaDownloader(HttpClient httpClient, string cacheRoot)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheRoot = cacheRoot ?? throw new ArgumentNullException(nameof(cacheRoot));
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
        var id = GetUniqueId(info);
        var folder = string.IsNullOrWhiteSpace(info.Category) ? "others" : info.Category;
        var dir = Path.Combine(_cacheRoot, folder, id);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, info.FileName);
    }

    // Partial file path used during download
    private string GetPartialFilePath(string finalPath) => finalPath + ".part";

    public bool IsCached(Responses.Media.MediaResponse info) => File.Exists(GetCachedFilePath(info));

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
        await foreach (var chunk in DownloadAsync(info, storageUri, session, cancellationToken))
        {
            progress?.Report(new(
                chunk.TotalDownloaded,
                chunk.FileSize,
                chunk.InstantaneousSpeedBytesPerSecond));
        }

        var finalPath = GetCachedFilePath(info);
        if (!File.Exists(finalPath))
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
        // Direct storage link used for this download (from server StoragePath)
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
    public sealed class DownloadSession
    {
        private int _paused;
        private readonly CancellationTokenSource _cts = new();

        public CancellationToken CancellationToken => _cts.Token;

        public void Pause() => Interlocked.Exchange(ref _paused, 1);
        public void Resume() => Interlocked.Exchange(ref _paused, 0);
        public bool IsPaused => Volatile.Read(ref _paused) != 0;
        public void Cancel() => _cts.Cancel();

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
    public async IAsyncEnumerable<DownloadChunk> DownloadAsync(Responses.Media.MediaResponse info, Uri storageUri, DownloadSession? session = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (info is null) throw new ArgumentNullException(nameof(info));
        if (storageUri is null) throw new ArgumentNullException(nameof(storageUri));

        var finalPath = GetCachedFilePath(info);
        var partialPath = GetPartialFilePath(finalPath);

        // If final file exists, yield nothing (already present). Caller can check existence first if desired.
        if (File.Exists(finalPath))
        {
            // File already fully downloaded and present in cache.
            // Yield a final DownloadChunk so callers receive the local path immediately.
            var fi = new FileInfo(finalPath);
            var existingTotal = fi.Length;
            yield return new DownloadChunk(Array.Empty<byte>(), 0, existingTotal, 0, existingTotal, finalPath, true);
            yield break;
        }


        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        long existing = 0;
        if (File.Exists(partialPath))
        {
            var fi = new FileInfo(partialPath);
            existing = fi.Length;
        }

        // Use provided values but validate
        var chunkSize = Math.Max(1, info.ChunkSize);
        var fileSize = storageUri.IsFile
            ? new FileInfo(storageUri.LocalPath).Length
            : Math.Max(0L, info.FileSize);
        var totalChunks = (int)Math.Max(1, Math.Ceiling(fileSize / (double)chunkSize));

        var startChunk = (int)(existing / chunkSize);

        // Open file with async options
        using var fs = new FileStream(partialPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        long totalDownloaded = existing;

        // Combine cancellation tokens
        CancellationToken linkedToken;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session?.CancellationToken ?? CancellationToken.None);
        linkedToken = cts.Token;

        for (int chunkIndex = startChunk; chunkIndex < totalChunks; chunkIndex++)
        {
            linkedToken.ThrowIfCancellationRequested();

            if (session != null)
            {
                await session.WaitWhilePausedAsync(linkedToken).ConfigureAwait(false);
            }

            var rangeStart = (long)chunkIndex * chunkSize;
            var rangeEnd = Math.Min(rangeStart + chunkSize - 1, fileSize - 1);
            if (rangeStart > rangeEnd) yield break;

            var attempt = 0;
            var maxAttempts = 6;
            Exception? lastEx = null;
            while (true)
            {
                linkedToken.ThrowIfCancellationRequested();
                attempt++;
                byte[]? outBuf = null;
                int bytesRead = 0;
                double speed = 0;
                try
                {
                    var sw = Stopwatch.StartNew();
                    Stream stream;
                    HttpRequestMessage? requestMessage = null;
                    HttpResponseMessage? responseMessage = null;

                    if (storageUri.IsFile)
                    {
                        stream = new FileStream(
                            storageUri.LocalPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            81920,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);

                        stream.Seek(rangeStart, SeekOrigin.Begin);
                    }
                    else
                    {
                        requestMessage = new global::System.Net.Http.HttpRequestMessage(global::System.Net.Http.HttpMethod.Get, storageUri.ToString());
                        requestMessage.Headers.Range = new RangeHeaderValue(rangeStart, rangeEnd);

                        responseMessage = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, linkedToken).ConfigureAwait(false);
                        responseMessage.EnsureSuccessStatusCode();

                        stream = await responseMessage.Content.ReadAsStreamAsync(linkedToken).ConfigureAwait(false);
                    }

                    try
                    {
                        await using (stream)
                        {
                            var expected = (int)(rangeEnd - rangeStart + 1);
                            var buffer = ArrayPool<byte>.Shared.Rent(expected);
                            try
                            {
                                int offset = 0;
                                while (offset < expected)
                                {
                                    var read = await stream.ReadAsync(buffer, offset, expected - offset, linkedToken).ConfigureAwait(false);
                                    if (read == 0) break;
                                    offset += read;
                                }

                                // Write into file at position rangeStart
                                fs.Seek(rangeStart, SeekOrigin.Begin);
                                await fs.WriteAsync(buffer.AsMemory(0, offset), linkedToken).ConfigureAwait(false);
                                await fs.FlushAsync(linkedToken).ConfigureAwait(false);

                                sw.Stop();
                                var elapsed = sw.Elapsed.TotalSeconds;
                                speed = elapsed > 0 ? offset / elapsed : 0;

                                bytesRead = offset;
                                outBuf = new byte[bytesRead];
                                Array.Copy(buffer, outBuf, bytesRead);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
                    }
                    finally
                    {
                        responseMessage?.Dispose();
                        requestMessage?.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (attempt < maxAttempts)
                    {
                        var delayMs = Math.Min(10000, 200 * (1 << (attempt - 1)));
                        try
                        {
                            await Task.Delay(delayMs, linkedToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { throw; }
                        continue; // retry
                    }
                    // else rethrow below
                }

                if (outBuf != null)
                {
                    totalDownloaded += bytesRead;
                    var isLast = chunkIndex == totalChunks - 1;
                    var localPath = isLast ? finalPath : partialPath;
                    yield return new DownloadChunk(outBuf, chunkIndex, totalDownloaded, speed, fileSize, localPath, isLast);
                    break; // success - go to next chunk
                }

                // if we reach here and no outBuf produced, attempts exhausted
                if (lastEx != null)
                {
                    throw lastEx;
                }
            }
        }

        // finalize
        fs.Dispose();
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(partialPath, finalPath);
    }
}
