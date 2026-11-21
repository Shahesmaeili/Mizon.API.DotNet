namespace Mizon.API.Client.Operations.Media;

/// <summary>Stores independently verified pieces even when earlier portions of a media file are missing.</summary>
public sealed class VerifiedMediaPieceCache
{
    private readonly string _directory;

    public VerifiedMediaPieceCache(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        _directory = Path.Combine(Path.GetFullPath(cacheDirectory), "verified-peer-pieces");
    }

    public async ValueTask<bool> StoreAsync(VerifiedMediaManifest manifest, int index, ReadOnlyMemory<byte> piece,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!manifest.Verify(index, piece.Span)) return false;
        var path = GetPath(manifest, index);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
                await file.WriteAsync(piece, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(staging, path, overwrite: true);
            return true;
        }
        finally { File.Delete(staging); }
    }

    public async ValueTask<bool> TryReadAsync(VerifiedMediaManifest manifest, int index, Memory<byte> target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var length = manifest.GetPieceLength(index);
        if (length == 0 || length != target.Length) return false;
        try
        {
            await using var file = new FileStream(GetPath(manifest, index), FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (file.Length != length) return false;
            await file.ReadExactlyAsync(target, cancellationToken).ConfigureAwait(false);
            return manifest.Verify(index, target.Span);
        }
        catch (IOException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    internal void Discard(VerifiedMediaManifest manifest, int index)
    {
        try { File.Delete(GetPath(manifest, index)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private string GetPath(VerifiedMediaManifest manifest, int index) =>
        Path.Combine(_directory, manifest.FileHash, manifest.PieceSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".piece");
}
