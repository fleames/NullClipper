using System.Diagnostics;
using System.IO;
using Drawing = System.Drawing;

namespace Clipper.Services;

/// <summary>
/// A local copy of each capture under <c>%AppData%\Clipper\snips\yyyy-MM-dd\</c>.
/// NullClipper's default output is the clipboard, which means a snip you forget to
/// paste is gone — this keeps the recent ones findable. Nothing leaves the machine,
/// and it is one checkbox away from off.
/// </summary>
public static class SnipHistoryService
{
    /// <summary>Files kept before the oldest are pruned. A few days of normal use.</summary>
    public const int MaxFiles = 60;

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Clipper",
        "snips");

    public static string? SaveImage(Drawing.Bitmap bitmap)
    {
        try
        {
            var path = NextPath("png");
            bitmap.Save(path, Drawing.Imaging.ImageFormat.Png);
            Trim();
            return path;
        }
        catch
        {
            // History is a convenience; never fail a capture over it.
            return null;
        }
    }

    public static string? SaveGif(byte[] bytes)
    {
        try
        {
            var path = NextPath("gif");
            File.WriteAllBytes(path, bytes);
            Trim();
            return path;
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<FileInfo> Recent(int max)
    {
        try
        {
            if (!Directory.Exists(Root))
            {
                return [];
            }

            return new DirectoryInfo(Root)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(file => file.Extension is ".png" or ".gif")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(max)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public static void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Root);
            Process.Start(new ProcessStartInfo
            {
                FileName = Root,
                UseShellExecute = true
            });
        }
        catch
        {
            // Explorer unavailable — nothing useful to say.
        }
    }

    public static void Trim()
    {
        try
        {
            var all = new DirectoryInfo(Root)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();

            foreach (var file in all.Skip(MaxFiles))
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Locked file; try again next time.
                }
            }
        }
        catch
        {
            // Pruning is best-effort.
        }
    }

    private static string NextPath(string extension)
    {
        var folder = Path.Combine(Root, DateTime.Now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(folder);

        var stem = DateTime.Now.ToString("HHmmss-fff");
        var path = Path.Combine(folder, $"{stem}.{extension}");
        for (var suffix = 2; File.Exists(path) && suffix < 100; suffix++)
        {
            path = Path.Combine(folder, $"{stem}-{suffix}.{extension}");
        }

        return path;
    }
}
