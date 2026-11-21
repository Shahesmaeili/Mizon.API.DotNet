using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mizon.API.Client.Operations.Media;

/// <summary>Transfers a single verified public media piece over a pinned TLS connection.</summary>
public static class LanMediaTransferProtocol
{
    private static ReadOnlySpan<byte> Magic => "MZP1"u8;
    private const int RequestSize = 44;

    public delegate ValueTask<int> ReadPublicPiece(string hash, int pieceSize, int index, Memory<byte> target, CancellationToken token);

    public static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Mizon local media", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, critical: false));
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
        if (!OperatingSystem.IsWindows()) return certificate;

        // Schannel cannot authenticate with the ephemeral private key returned by CertificateRequest.
        using (certificate)
        {
            var pfx = certificate.Export(X509ContentType.Pkcs12);
            try { return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.UserKeySet); }
            finally { CryptographicOperations.ZeroMemory(pfx); }
        }
    }

    public static async ValueTask<bool> TryReadAsync(IPEndPoint endpoint, byte[] certificateSha256, VerifiedMediaManifest manifest,
        int index, Memory<byte> destination, CancellationToken token)
    {
        if (certificateSha256.Length != SHA256.HashSizeInBytes || destination.Length == 0
            || destination.Length != manifest.GetPieceLength(index) || !IsLocal(endpoint.Address)) return false;

        using var client = new TcpClient(endpoint.AddressFamily);
        await client.ConnectAsync(endpoint, token).ConfigureAwait(false);
        using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
            (_, certificate, _, _) => certificate is not null && CryptographicOperations.FixedTimeEquals(
                certificate.GetCertHash(HashAlgorithmName.SHA256), certificateSha256));
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "mizon-local-media" }, token).ConfigureAwait(false);

        var request = new byte[RequestSize];
        Magic.CopyTo(request);
        Convert.FromHexString(manifest.FileHash).CopyTo(request, 4);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(36, 4), manifest.PieceSize);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(40, 4), index);
        await tls.WriteAsync(request, token).ConfigureAwait(false);
        var length = request.AsMemory(0, 4);
        await tls.ReadExactlyAsync(length, token).ConfigureAwait(false);
        if (BinaryPrimitives.ReadInt32BigEndian(length.Span) != destination.Length) return false;
        await tls.ReadExactlyAsync(destination, token).ConfigureAwait(false);
        return manifest.Verify(index, destination.Span);
    }

    public static async ValueTask<int> ServeAsync(Stream transport, X509Certificate2 certificate, ReadPublicPiece readPiece, CancellationToken token)
    {
        using var tls = new SslStream(transport, leaveInnerStreamOpen: true);
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, token).ConfigureAwait(false);
        var request = new byte[RequestSize];
        await tls.ReadExactlyAsync(request, token).ConfigureAwait(false);
        if (!request.AsSpan(0, 4).SequenceEqual(Magic)) return 0;
        var pieceSize = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(36, 4));
        var index = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(40, 4));
        if (pieceSize is < 1 or > VerifiedMediaManifest.MaximumPieceSize || index is < 0 or >= VerifiedMediaManifest.MaximumPieces) return 0;
        var buffer = ArrayPool<byte>.Shared.Rent(pieceSize);
        try
        {
            var count = await readPiece(Convert.ToHexStringLower(request.AsSpan(4, 32)), pieceSize, index,
                buffer.AsMemory(0, pieceSize), token).ConfigureAwait(false);
            if (count < 0 || count > pieceSize) return 0;
            await WriteLengthAsync(tls, count, token).ConfigureAwait(false);
            if (count > 0) await tls.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            return count;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static ValueTask WriteLengthAsync(Stream response, int length, CancellationToken token)
    {
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(prefix, length);
        return response.WriteAsync(prefix, token);
    }

    public static bool IsLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal) return true;
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out var length);
        return length == 16 ? (bytes[0] & 0xfe) == 0xfc
            : bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254);
    }
}
