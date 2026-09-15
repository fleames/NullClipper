using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Clipper.Services.NullImage;

/// <summary>
/// Byte-for-byte port of NullImage's browser-side crypto (scheme
/// <c>ni1-aes-gcm-256</c>, see nullimage's docs/nullimage/crypto-spec.md and
/// lib/client/crypto.ts). The wire format and AAD binding here must match
/// that implementation exactly, or the web viewer cannot decrypt what this
/// uploads and vice versa.
///
/// Wire format for the ciphertext blob:
///   header: "NIR1" (4) | version(1)=1 | flags(1)=0 | chunkSize(4, BE) | reserved(2)
///   chunk*: iv(12) | ciphertext(len) | tag(16)
/// </summary>
internal static class NullImageCrypto
{
    public const string EncryptionScheme = "ni1-aes-gcm-256";
    public const int IvBytes = 12;
    public const int TagBytes = 16;
    public const int HeaderBytes = 12;
    public const int DefaultChunkSize = 2 * 1024 * 1024;

    private static readonly byte[] Magic = "NIR1"u8.ToArray();

    public static byte[] GenerateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] BuildHeader(int chunkSize)
    {
        var header = new byte[HeaderBytes];
        Magic.CopyTo(header, 0);
        header[4] = 1; // version
        header[5] = 0; // flags — reserved for future scheme variants
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(6, 4), (uint)chunkSize);
        // header[10..12] reserved, left zero
        return header;
    }

    /// <summary>Positional binding for a chunk — any reorder, splice, or truncation breaks GCM auth.</summary>
    public static byte[] ChunkAad(string imageId, int chunkIndex, int totalChunks) =>
        Encoding.UTF8.GetBytes($"ni1|{imageId}|{chunkIndex}|{totalChunks}");

    public static int TotalChunksFor(long plaintextSize, int chunkSize) =>
        (int)Math.Max(1, (plaintextSize + chunkSize - 1) / chunkSize);

    /// <summary>Exact ciphertext length for a plaintext of this size — the server validates against this independently.</summary>
    public static long CiphertextSizeFor(long plaintextSize, int chunkSize = DefaultChunkSize)
    {
        var chunks = TotalChunksFor(plaintextSize, chunkSize);
        return HeaderBytes + plaintextSize + (long)chunks * (IvBytes + TagBytes);
    }

    /// <summary>Encrypts one chunk, returning <c>iv || ciphertext || tag</c>, ready to append to the blob.</summary>
    public static byte[] EncryptChunk(byte[] key, ReadOnlySpan<byte> plaintext, string imageId, int chunkIndex, int totalChunks)
    {
        var iv = new byte[IvBytes];
        RandomNumberGenerator.Fill(iv);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using (var aes = new AesGcm(key, TagBytes))
        {
            aes.Encrypt(iv, plaintext, ciphertext, tag, ChunkAad(imageId, chunkIndex, totalChunks));
        }

        var framed = new byte[IvBytes + ciphertext.Length + TagBytes];
        iv.CopyTo(framed, 0);
        ciphertext.CopyTo(framed, IvBytes);
        tag.CopyTo(framed, IvBytes + ciphertext.Length);
        return framed;
    }

    /// <summary>
    /// Encrypts <c>{name,type,size}</c> for the <c>x-nullimage-metadata</c> header value:
    /// <c>b64url(iv).b64url(ciphertext+tag)</c>. Bound to the image id so it cannot be replayed elsewhere.
    /// </summary>
    public static string EncryptMetadata(byte[] key, string imageId, string name, string type, long size)
    {
        var json = JsonSerializer.Serialize(new { name, type, size });
        var plaintext = Encoding.UTF8.GetBytes(json);
        var iv = new byte[IvBytes];
        RandomNumberGenerator.Fill(iv);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        var aad = Encoding.UTF8.GetBytes($"ni1-meta|{imageId}");
        using (var aes = new AesGcm(key, TagBytes))
        {
            aes.Encrypt(iv, plaintext, ciphertext, tag, aad);
        }

        var combined = new byte[ciphertext.Length + TagBytes];
        ciphertext.CopyTo(combined, 0);
        tag.CopyTo(combined, ciphertext.Length);

        return $"{Base64UrlEncode(iv)}.{Base64UrlEncode(combined)}";
    }
}
