using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Clipper.Models;

namespace Clipper.Services.NullImage;

/// <summary>Expiry presets, mirroring nullimage's lib/config.ts <c>EXPIRATION_OPTIONS</c> exactly.</summary>
public static class NullImageExpiryPresets
{
    public const string Default = "1d";

    public static readonly IReadOnlyList<(string Value, string Label)> All =
    [
        ("1h", "1 hour"),
        ("1d", "1 day"),
        ("3d", "3 days"),
        ("7d", "7 days"),
        ("never", "Never"),
    ];

    public static long? SecondsFor(string? preset) => preset switch
    {
        "1h" => 60 * 60,
        "3d" => 3 * 24 * 60 * 60,
        "7d" => 7 * 24 * 60 * 60,
        "never" => null,
        _ => 24 * 60 * 60, // "1d", and the fallback for anything unrecognized
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
        var bytes = stream.ToArray();

        var client = new NullImageClient(ServerUrl);
        var options = new NullImageUploadOptions
        {
            ExpirySeconds = NullImageExpiryPresets.SecondsFor(settings.NullImageExpiry),
            BurnAfterView = settings.NullImageBurnAfterView,
            Password = string.IsNullOrWhiteSpace(settings.NullImagePassword) ? null : settings.NullImagePassword,
        };

        return await client
            .UploadAsync(bytes, $"clip-{DateTime.Now:yyyyMMdd-HHmmss}.png", "image/png", options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
