using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mizon.API.Client.Operations.Media;
using Mizon.API.Client.Responses.Media;

internal static partial class MediaChecks
{
    private static async Task VerifyPeerPiecesAsync(string root, byte[] data, Uri origin)
    {
        var info = Info(data.Length);
        info.FileHash = Convert.ToHexStringLower(SHA256.HashData(data));
        info.TotalChunks = (data.Length + info.ChunkSize - 1) / info.ChunkSize;
        var hashes = data.Chunk(info.ChunkSize).SelectMany(static piece => SHA256.HashData(piece)).ToArray();
        var manifest = VerifiedMediaManifest.Create(info.FileHash, info.FileSize, info.ChunkSize, hashes)!;
        var windows = new VerifiedMediaPieceCache(Path.Combine(root, "peer-Windows"));
        var mac = new VerifiedMediaPieceCache(Path.Combine(root, "peer-Mac"));
        var nodes = new TestPeers(manifest, windows, mac);

        for (var index = 0; index < 4; index++)
            Check(await windows.StoreAsync(manifest, index, data.AsMemory(index * info.ChunkSize, info.ChunkSize)),
                "First peer can seed a verified prefix piece");
        for (var index = 4; index < 7; index++)
            Check(await mac.StoreAsync(manifest, index, data.AsMemory(index * info.ChunkSize, info.ChunkSize)),
                "Second peer can seed a verified middle piece without the prefix");
        Check(!await mac.TryReadAsync(manifest, 0, new byte[info.ChunkSize]), "Sparse peer has no fabricated prefix");
        Check(!await mac.StoreAsync(manifest, 8, new byte[info.ChunkSize]), "Corrupted bytes are not published as a seed piece");

        using var originHandler = new MediaHandler(data);
        using var originClient = new HttpClient(originHandler);
        var target = new MediaDownloader(originClient, Path.Combine(root, "peer-receiver"), nodes);
        var finalPath = await target.DownloadToCacheAsync(info, origin);
        Check((await File.ReadAllBytesAsync(finalPath)).SequenceEqual(data) && nodes.Hits == 7 && originHandler.Requests == 3,
            "Seven pieces come from two peers and only the three missing pieces come from the origin");
        await VerifyTlsPeersAsync(root, data, origin, info, manifest, windows, mac);
        var cachedPiece = new byte[manifest.GetPieceLength(9)];
        Check(await target.TryReadCachedPieceAsync(info, manifest, 9, cachedPiece) && cachedPiece.SequenceEqual(data[^cachedPiece.Length..]),
            "The receiving client can serve an exact, verified final piece");

        using var corruptHandler = new MediaHandler(data);
        using var corruptClient = new HttpClient(corruptHandler);
        var corruptPeer = new TestPeers(manifest, windows, mac) { CorruptFirst = true };
        var fallback = new MediaDownloader(corruptClient, Path.Combine(root, "peer-bad"), corruptPeer);
        Check((await File.ReadAllBytesAsync(await fallback.DownloadToCacheAsync(info, origin))).SequenceEqual(data)
            && corruptHandler.Requests == 4, "A forged piece is rejected before playback or cache write and falls back to origin");

        using var resumeHandler = new MediaHandler(data);
        using var resumeClient = new HttpClient(resumeHandler);
        var available = new MediaDownloader(resumeClient, Path.Combine(root, "peer-resumed"), new TestPeers(manifest));
        var prefix = data[..(2 * info.ChunkSize)];
        prefix[^1] ^= 3;
        await File.WriteAllBytesAsync(available.GetCachedFilePath(info) + ".part", prefix);
        Check(await available.TryReadCachedPieceAsync(info, manifest, 0, new byte[info.ChunkSize])
            && !await available.TryReadCachedPieceAsync(info, manifest, 1, new byte[info.ChunkSize]),
            "Only valid pieces are served from a partial cache");
        Check((await File.ReadAllBytesAsync(await available.DownloadToCacheAsync(info, origin))).SequenceEqual(data)
            && resumeHandler.Requests == 9, "A corrupt resumed prefix is trimmed at the last verified piece");
        await VerifyPriorityWithoutRangesAsync(root, data, origin, info, manifest);
    }

    private static async Task VerifyPriorityWithoutRangesAsync(string root, byte[] data, Uri origin,
        MediaResponse info, VerifiedMediaManifest manifest)
    {
        var receiverRoot = Path.Combine(root, "peer-no-ranges");
        var local = new VerifiedMediaPieceCache(receiverRoot);
        var peer = new VerifiedMediaPieceCache(Path.Combine(root, "peer-no-ranges-neighbor"));
        var last = manifest.PieceCount - 1;
        var shared = data.AsMemory(info.ChunkSize, info.ChunkSize);
        Check(await local.StoreAsync(manifest, 1, shared) &&
            await peer.StoreAsync(manifest, 1, shared) &&
            await peer.StoreAsync(manifest, 2, data.AsMemory(2 * info.ChunkSize, info.ChunkSize)) &&
            await peer.StoreAsync(manifest, last, data.AsMemory(last * info.ChunkSize)),
            "Receiver and neighbor contain overlapping and distinct verified pieces");

        using var handler = new MediaHandler(data) { IgnoreRange = true };
        using var http = new HttpClient(handler);
        var neighbors = new TestPeers(manifest, peer);
        var receiver = new MediaDownloader(http, receiverRoot, neighbors);
        var result = await receiver.DownloadToCacheAsync(info, origin);
        Check((await File.ReadAllBytesAsync(result)).SequenceEqual(data) &&
            neighbors.Hits == 2 && handler.Requests == 1 && handler.Disposals == 1 &&
            handler.BodyBytes == last * info.ChunkSize,
            "Each piece prefers local cache then LAN even when the server provides one continuous HTTP 200 body");
        Check(await receiver.DownloadToCacheAsync(info, origin) == result &&
            neighbors.Hits == 2 && handler.Requests == 1,
            "A completed device cache satisfies the next download");
    }

    private static async Task VerifyTlsPeersAsync(string root, byte[] data, Uri origin, MediaResponse info,
        VerifiedMediaManifest manifest, VerifiedMediaPieceCache first, VerifiedMediaPieceCache second)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using var windows = new LoopbackPeer(manifest, first, stop.Token);
        await using var mac = new LoopbackPeer(manifest, second, stop.Token);
        using var originHandler = new MediaHandler(data);
        using var originClient = new HttpClient(originHandler);
        var receiver = new MediaDownloader(originClient, Path.Combine(root, "peer-tls"), new NetworkPeers(manifest, windows, mac));
        var path = await receiver.DownloadToCacheAsync(info, origin, cancellationToken: stop.Token);
        Check((await File.ReadAllBytesAsync(path, stop.Token)).SequenceEqual(data)
            && originHandler.Requests == 3 && windows.BytesSent == 4 * info.ChunkSize && mac.BytesSent == 3 * info.ChunkSize,
            $"Two actual pinned TLS peers and HTTP ranges reconstruct the same media (origin={originHandler.Requests}, first={windows.BytesSent}, second={mac.BytesSent})");

        await ThrowsAsync<System.Security.Authentication.AuthenticationException>(
            () => LanMediaTransferProtocol.TryReadAsync(windows.Endpoint, new byte[32], manifest,
                0, new byte[info.ChunkSize], stop.Token).AsTask(), "Peer TLS rejects a mismatched certificate fingerprint");
    }

    private sealed class LoopbackPeer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly X509Certificate2 _certificate = LanMediaTransferProtocol.CreateCertificate();
        private readonly CancellationTokenSource _stop;
        private readonly Task _work;
        internal byte[] Fingerprint => _certificate.GetCertHash(HashAlgorithmName.SHA256);
        internal IPEndPoint Endpoint => (IPEndPoint)_listener.LocalEndpoint;
        internal int BytesSent;

        internal LoopbackPeer(VerifiedMediaManifest manifest, VerifiedMediaPieceCache pieces, CancellationToken token)
        {
            _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            _listener.Start();
            _work = RunAsync(manifest, pieces);
        }

        private async Task RunAsync(VerifiedMediaManifest manifest, VerifiedMediaPieceCache pieces)
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    try
                    {
                        BytesSent += await LanMediaTransferProtocol.ServeAsync(client.GetStream(), _certificate,
                            async (hash, size, index, memory, token) =>
                            {
                                var length = manifest.GetPieceLength(index);
                                if (hash != manifest.FileHash || size != manifest.PieceSize || length == 0) return 0;
                                return await pieces.TryReadAsync(manifest, index, memory[..length], token) ? length : 0;
                            }, _stop.Token);
                    }
                    catch (Exception error) when (error is IOException or System.Security.Authentication.AuthenticationException) { }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            try { await _work; }
            finally
            {
                _listener.Stop();
                _stop.Dispose();
                _certificate.Dispose();
            }
        }
    }

    private sealed class NetworkPeers(VerifiedMediaManifest manifest, params LoopbackPeer[] remote) : IMediaPeerSource
    {
        public bool EnableLookahead => true;
        public ValueTask<VerifiedMediaManifest?> GetManifestAsync(MediaResponse media, CancellationToken cancellationToken)
            => ValueTask.FromResult<VerifiedMediaManifest?>(manifest);

        public async ValueTask<bool> TryReadPieceAsync(VerifiedMediaManifest table, int index, Memory<byte> destination,
            CancellationToken token)
        {
            foreach (var node in remote)
                if (await LanMediaTransferProtocol.TryReadAsync(node.Endpoint, node.Fingerprint, table, index, destination, token))
                    return true;
            return false;
        }
    }

    private sealed class TestPeers(VerifiedMediaManifest manifest, params VerifiedMediaPieceCache[] nodes) : IMediaPeerSource
    {
        public int Hits { get; private set; }
        public bool CorruptFirst { get; init; }

        public ValueTask<VerifiedMediaManifest?> GetManifestAsync(MediaResponse media, CancellationToken cancellationToken)
            => ValueTask.FromResult<VerifiedMediaManifest?>(manifest);

        public async ValueTask<bool> TryReadPieceAsync(VerifiedMediaManifest media, int index, Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            foreach (var node in nodes)
            {
                if (!await node.TryReadAsync(media, index, destination, cancellationToken)) continue;
                if (index == 0 && CorruptFirst) destination.Span[0] ^= 1;
                Hits++;
                return true;
            }
            return false;
        }
    }
}
