using Microsoft.Win32;

namespace Clipper.Services;

public static class StartupService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NullClipper";
    private const string LegacyValueName = "Clipper";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string
            || key?.GetValue(LegacyValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);

        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                key.SetValue(ValueName, $"\"{exe}\"");
                key.DeleteValue(LegacyValueName, false);
            }
        }
        else
        {
            key.DeleteValue(ValueName, false);
            key.DeleteValue(LegacyValueName, false);
        }
    }
}
