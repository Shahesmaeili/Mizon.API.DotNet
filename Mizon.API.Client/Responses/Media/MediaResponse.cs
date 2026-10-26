namespace Mizon.API.Client.Responses.Media;

public sealed class MediaResponse : IApiResponse
{
    public Guid MediaPublicId { get; set; }
    public int TotalChunks { get; set; }
    public int ChunkSize { get; set; }
    public string FileHash { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    // Direct HTTPS link to the storage (may be on different server)
    public string StoragePath { get; set; } = null!;
    // Category is provided by server (e.g. images, videos, music, others)
    public string Category { get; set; } = null!;
}
