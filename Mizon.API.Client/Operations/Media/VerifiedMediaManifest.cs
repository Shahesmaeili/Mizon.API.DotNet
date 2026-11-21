using System.Security.Cryptography;
using Mizon.API.Client.Responses.Media;

namespace Mizon.API.Client.Operations.Media;

/// <summary>An immutable piece table obtained from a trusted media origin, never from a peer.</summary>
public sealed class VerifiedMediaManifest
{
    public const int MaximumPieceSize = 4 * 1024 * 1024;
    public const int MaximumPieces = 65_536;
    private readonly byte[] _digests;

    private VerifiedMediaManifest(string fileHash, long length, int pieceSize, byte[] digests)
    {
        FileHash = fileHash;
        Length = length;
        PieceSize = pieceSize;
        _digests = digests;
    }

    public string FileHash { get; }
    public long Length { get; }
    public int PieceSize { get; }
    public int PieceCount => _digests.Length / SHA256.HashSizeInBytes;

    public static VerifiedMediaManifest? Create(string? fileHash, long length, int pieceSize, ReadOnlySpan<byte> packedSha256)
    {
        if (fileHash is not { Length: 64 } || !fileHash.All(Uri.IsHexDigit) || length <= 0
            || pieceSize is < 1 or > MaximumPieceSize) return null;
        var count = (length - 1) / pieceSize + 1;
        if (count > MaximumPieces || packedSha256.Length != count * SHA256.HashSizeInBytes) return null;
        return new(fileHash.ToLowerInvariant(), length, pieceSize, packedSha256.ToArray());
    }

    public bool Matches(MediaResponse media) => media.FileSize == Length && media.ChunkSize == PieceSize
        && media.TotalChunks == PieceCount && string.Equals(media.FileHash, FileHash, StringComparison.OrdinalIgnoreCase);

    public int GetPieceLength(int index) => index >= 0 && index < PieceCount
        ? (int)Math.Min(PieceSize, Length - (long)index * PieceSize) : 0;

    public bool Verify(int index, ReadOnlySpan<byte> data)
    {
        var expectedLength = GetPieceLength(index);
        if (expectedLength == 0 || data.Length != expectedLength) return false;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(data, hash);
        return CryptographicOperations.FixedTimeEquals(hash, _digests.AsSpan(index * hash.Length, hash.Length));
    }
}

/// <summary>The media client supplies discovery and LAN transport. Only the origin supplies the manifest.</summary>
public interface IMediaPeerSource
{
    ValueTask<VerifiedMediaManifest?> GetManifestAsync(MediaResponse media, CancellationToken cancellationToken);
    void ObserveCachedMedia(MediaResponse media) { }
    bool EnableLookahead => false;

    /// <summary>Tries peers in order. Copies a complete verified piece, or returns false.</summary>
    ValueTask<bool> TryReadPieceAsync(VerifiedMediaManifest manifest, int index, Memory<byte> destination,
        CancellationToken cancellationToken);
}
