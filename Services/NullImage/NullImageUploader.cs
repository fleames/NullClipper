using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Clipper.Models;

namespace Clipper.Services.NullImage;

/// <summary>Finite NullImage expiry presets (1h / 1d / 3d / 7d). "Never" is not offered.</summary>
public static class NullImageExpiryPresets
{
    public const string Default = "1d";

    public static readonly IReadOnlyList<(string Value, string Label)> All =
    [
        ("1h", "1 hour"),
        ("1d", "1 day"),
        ("3d", "3 days"),
        ("7d", "7 days"),
    ];

    public static long SecondsFor(string? preset) => preset switch
    {
        "1h" => 60 * 60,
        "3d" => 3 * 24 * 60 * 60,
        "7d" => 7 * 24 * 60 * 60,
        _ => 24 * 60 * 60, // "1d", former "never", and anything unrecognized
    };
}

/// <summary>Bridges a captured <see cref="Bitmap"/> and the app's settings to <see cref="NullImageClient"/>.</summary>
public static class NullImageUploader
{
    private const string ServerUrl = "https://nullimage.org";

    public static async Task<NullImageUploadResult> UploadCaptureAsync(
        Bitmap capture,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        capture.Save(stream, ImageFormat.Png);
        return await UploadBytesAsync(
            stream.ToArray(),
            $"clip-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            "image/png",
            settings,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<NullImageUploadResult> UploadBytesAsync(
        byte[] bytes,
        string fileName,
        string mimeType,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var client = new NullImageClient(ServerUrl);
        var options = new NullImageUploadOptions
        {
            ExpirySeconds = NullImageExpiryPresets.SecondsFor(settings.NullImageExpiry),
            BurnAfterView = settings.NullImageBurnAfterView,
            Password = string.IsNullOrWhiteSpace(settings.NullImagePassword) ? null : settings.NullImagePassword,
        };

        return await client
            .UploadAsync(bytes, fileName, mimeType, options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
