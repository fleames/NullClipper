using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clipper.Services.NullImage;

/// <summary>
/// Options for one upload. <see cref="ExpirySeconds"/> is <c>null</c> for the
/// server's "never expires" tier (nullimage's product-spec.md); pick a value
/// via <see cref="NullImageExpiryPresets"/>.
/// </summary>
public sealed record NullImageUploadOptions
{
    public long? ExpirySeconds { get; init; }
    public bool BurnAfterView { get; init; }
    public string? Password { get; init; }
}

public sealed record NullImageUploadResult
{
    public required string ImageId { get; init; }
    public required string ShareUrl { get; init; }
    public required string ManageToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>A NullImage API error — <see cref="Code"/> is the machine-readable code from the server's error envelope.</summary>
public sealed class NullImageException : Exception
{
    public string Code { get; }
    public int? StatusCode { get; }

    public NullImageException(string code, string message, int? statusCode = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

/// <summary>
/// A .NET client for NullImage's zero-knowledge upload API. Encrypts client-side
/// (AES-256-GCM, scheme <c>ni1-aes-gcm-256</c>) exactly like the web app does,
/// so links this produces open normally in a browser and the server never sees
/// plaintext image bytes, filename, or MIME type.
///
/// Flow: <c>POST /api/images</c> (reserve) → <c>PUT /api/images/:id/blob</c>
/// (stream ciphertext, authorized by a per-image capability token — there are
/// no accounts or API keys) → <c>POST /api/images/:id/finalize</c>.
/// </summary>
public sealed class NullImageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Uri _baseUrl;

    public NullImageClient(string baseUrl, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("A NullImage server URL is required.", nameof(baseUrl));
        }

        _baseUrl = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        _http = httpClient ?? new HttpClient();
    }

    /// <summary>Encrypts and uploads <paramref name="plaintext"/>, returning a share URL with the key in its fragment.</summary>
    public async Task<NullImageUploadResult> UploadAsync(
        byte[] plaintext,
        string fileName,
        string mimeType,
        NullImageUploadOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var key = NullImageCrypto.GenerateKey();
        var created = await CreateImageAsync(plaintext.LongLength, options, cancellationToken).ConfigureAwait(false);

        const int chunkSize = NullImageCrypto.DefaultChunkSize;
        var totalChunks = NullImageCrypto.TotalChunksFor(plaintext.LongLength, chunkSize);

        using var body = new MemoryStream();
        body.Write(NullImageCrypto.BuildHeader(chunkSize));
        for (var index = 0; index < totalChunks; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index * chunkSize;
            var length = Math.Min(chunkSize, plaintext.Length - start);
            var framed = NullImageCrypto.EncryptChunk(key, plaintext.AsSpan(start, length), created.Id, index, totalChunks);
            body.Write(framed);
            progress?.Report((double)(index + 1) / totalChunks);
        }

        var metadata = NullImageCrypto.EncryptMetadata(key, created.Id, fileName, mimeType, plaintext.LongLength);

        await PutBlobAsync(created.Id, created.UploadToken, body.ToArray(), metadata, plaintext.LongLength, cancellationToken)
            .ConfigureAwait(false);
        await FinalizeAsync(created.Id, created.UploadToken, cancellationToken).ConfigureAwait(false);

        return new NullImageUploadResult
        {
            ImageId = created.Id,
            ShareUrl = $"{_baseUrl}{created.Id}#k={NullImageCrypto.Base64UrlEncode(key)}",
            ManageToken = created.ManageToken,
            ExpiresAt = created.ExpiresAt,
        };
    }

    private async Task<CreateImageResponse> CreateImageAsync(
        long plaintextSize,
        NullImageUploadOptions options,
        CancellationToken cancellationToken)
    {
        var payload = new CreateImageRequest
        {
            DeclaredPlaintextSize = plaintextSize,
            ExpirySeconds = options.ExpirySeconds,
            BurnAfterView = options.BurnAfterView,
            Password = string.IsNullOrEmpty(options.Password) ? null : options.Password,
            EncryptionScheme = NullImageCrypto.EncryptionScheme,
        };

        using var response = await _http
            .PostAsJsonAsync(new Uri(_baseUrl, "api/images"), payload, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        var created = await response.Content.ReadFromJsonAsync<CreateImageResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return created ?? throw new NullImageException("SERVER_ERROR", "The server returned an empty response.");
    }

    private async Task PutBlobAsync(
        string imageId,
        string uploadToken,
        byte[] body,
        string metadata,
        long plaintextSize,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(_baseUrl, $"api/images/{imageId}/blob"));
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Add("x-nullimage-upload-token", uploadToken);
        request.Headers.Add("x-nullimage-metadata", metadata);
        request.Headers.Add("x-nullimage-plaintext-size", plaintextSize.ToString());

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FinalizeAsync(string imageId, string uploadToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUrl, $"api/images/{imageId}/finalize"));
        request.Headers.Add("x-nullimage-upload-token", uploadToken);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<NullImageException> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (body?.Error is { } error)
            {
                return new NullImageException(
                    error.Code ?? "SERVER_ERROR",
                    error.Message ?? "The request failed.",
                    (int)response.StatusCode);
            }
        }
        catch
        {
            // Non-JSON error body (e.g. the blob route's plain-text error responses).
        }

        return new NullImageException(
            "SERVER_ERROR",
            $"The request failed with status {(int)response.StatusCode}.",
            (int)response.StatusCode);
    }

    private sealed class CreateImageRequest
    {
        [JsonPropertyName("declaredPlaintextSize")]
        public required long DeclaredPlaintextSize { get; init; }

        [JsonPropertyName("expirySeconds")]
        public long? ExpirySeconds { get; init; }

        [JsonPropertyName("burnAfterView")]
        public bool BurnAfterView { get; init; }

        // The server's schema declares this field `optional()` but not `nullable()` —
        // it must be omitted entirely when there is no password, not sent as `null`,
        // or validation fails with "expected string, received null".
        [JsonPropertyName("password")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Password { get; init; }

        [JsonPropertyName("encryptionScheme")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EncryptionScheme { get; init; }
    }

    private sealed class CreateImageResponse
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("manageToken")]
        public required string ManageToken { get; init; }

        [JsonPropertyName("uploadToken")]
        public required string UploadToken { get; init; }

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset? ExpiresAt { get; init; }
    }

    private sealed class ApiErrorEnvelope
    {
        [JsonPropertyName("error")]
        public ApiErrorBody? Error { get; init; }
    }

    private sealed class ApiErrorBody
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
