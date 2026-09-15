using System.IO;
using System.Text.Json;
using Clipper.Models;

namespace Clipper.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public SettingsStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clipper");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                Current.Normalize();
            }
        }
        catch
        {
            Current = new AppSettings();
        }

        return Current;
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
