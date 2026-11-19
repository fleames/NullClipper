using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Clipper.Services;

/// <summary>
/// Opt-in release check. Off unless the user ticks it in Settings, because the privacy
/// copy promises the app's only network call is the optional NullImage upload — a check
/// that runs by default would make that untrue (GitHub still sees the request and the IP).
/// </summary>
public static class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/fleames/NullClipper/releases/latest";

    public static string ReleasesPageUrl => "https://github.com/fleames/NullClipper/releases/latest";

    public static Version CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
        }
    }

    /// <summary>Returns the release tag when it is newer than this build, otherwise null.</summary>
    public static async Task<string?> GetNewerTagAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"NullClipper/{CurrentVersion}");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await http.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var release = await response.Content
                .ReadFromJsonAsync<ReleaseInfo>(cancellationToken)
                .ConfigureAwait(false);

            var tag = release?.TagName;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            return Version.TryParse(tag.TrimStart('v', 'V'), out var latest) && latest > CurrentVersion
                ? tag
                : null;
        }
        catch
        {
            // Offline, rate-limited, or GitHub changed shape — all the same to the user.
            return null;
        }
    }

    private sealed class ReleaseInfo
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
    }
}
