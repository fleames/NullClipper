using Clipper.Native;
using System.Windows.Input;

namespace Clipper.Models;

public sealed class AppSettings
{
    public uint HotkeyModifiers { get; set; } = NativeMethods.ModControl | NativeMethods.ModShift;
    public uint HotkeyVirtualKey { get; set; } = 0x58;
    public bool StartWithWindows { get; set; }

    /// <summary>Legacy preset id from earlier builds; migrated on load.</summary>
    public string? HotkeyId { get; set; }

    /// <summary>When true, a snip is uploaded to NullImage instead of copied as an image, and the share link is copied instead.</summary>
    public bool NullImageEnabled { get; set; }
    public string NullImageExpiry { get; set; } = "1d";
    public bool NullImageBurnAfterView { get; set; }
    public string? NullImagePassword { get; set; }

    /// <summary>
    /// Last capture kind: "snip" or "gif". GIF v1 uses the same region overlay, then records
    /// that frozen-size rectangle (CopyFromScreen; overlay chrome is not captured) at 12 fps
    /// until Stop, Esc, or 8s. Longest edge is capped at 640px and encoded with AnimatedGif.
    /// Output matches snip: clipboard and/or NullImage (image/gif).
    /// </summary>
    public string CaptureMode { get; set; } = "snip";

    public bool IsGifMode => string.Equals(CaptureMode, "gif", StringComparison.OrdinalIgnoreCase);

    public HotkeyBinding Hotkey => new(HotkeyModifiers, HotkeyVirtualKey);

    public void SetHotkey(HotkeyBinding binding)
    {
        HotkeyModifiers = binding.Modifiers;
        HotkeyVirtualKey = binding.VirtualKey;
        HotkeyId = null;
    }

    public void MigrateLegacyHotkey()
    {
        if (HotkeyVirtualKey != 0)
        {
            return;
        }

        var legacy = HotkeyPresets.Get(HotkeyId);
        HotkeyModifiers = legacy.Modifiers;
        HotkeyVirtualKey = legacy.VirtualKey;
        HotkeyId = null;
    }

    public void Normalize()
    {
        MigrateLegacyHotkey();
        if (!IsGifMode)
        {
            CaptureMode = "snip";
        }

        if (string.Equals(NullImageExpiry, "never", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(NullImageExpiry)
            || NullImageExpiry is not ("1h" or "1d" or "3d" or "7d"))
        {
            NullImageExpiry = "1d";
        }
    }
}

public readonly record struct HotkeyBinding(uint Modifiers, uint VirtualKey)
{
    public static HotkeyBinding Default { get; } = new(
        NativeMethods.ModControl | NativeMethods.ModShift,
        0x58);

    public string Label => HotkeyLabel.Format(Modifiers, VirtualKey);
}

public sealed record HotkeyPreset(string Id, string Label, uint Modifiers, uint VirtualKey);

public static class HotkeyPresets
{
    public const string DefaultId = "ctrl-shift-x";

    public static readonly IReadOnlyList<HotkeyPreset> All =
    [
        new(DefaultId, "Ctrl + Shift + X", NativeMethods.ModControl | NativeMethods.ModShift, 0x58),
        new("win-shift-s", "Win + Shift + S", NativeMethods.ModWin | NativeMethods.ModShift, 0x53),
        new("print-screen", "Print Screen", 0, 0x2C),
        new("ctrl-print-screen", "Ctrl + Print Screen", NativeMethods.ModControl, 0x2C),
        new("f9", "F9", 0, 0x78)
    ];

    public static HotkeyPreset Get(string? id) =>
        All.FirstOrDefault(p => p.Id == id) ?? All[0];
}

public static class HotkeyLabel
{
    public static List<string> Parts(uint modifiers, uint virtualKey, bool includeKey = true)
    {
        var parts = new List<string>();
        if ((modifiers & NativeMethods.ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & NativeMethods.ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & NativeMethods.ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & NativeMethods.ModWin) != 0)
        {
            parts.Add("Win");
        }

        if (includeKey)
        {
            parts.Add(KeyName(virtualKey));
        }

        return parts;
    }

    public static string Format(uint modifiers, uint virtualKey) =>
        string.Join(" + ", Parts(modifiers, virtualKey));

    public static string KeyName(uint virtualKey)
    {
        return virtualKey switch
        {
            0x2C => "Print Screen",
            0x13 => "Pause",
            0x91 => "Scroll Lock",
            0x90 => "Num Lock",
            _ => FormatWpfKey(virtualKey)
        };
    }

    private static string FormatWpfKey(uint virtualKey)
    {
        try
        {
            var key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
            if (key is Key.None or Key.DeadCharProcessed)
            {
                return $"Key {virtualKey}";
            }

            var text = new KeyConverter().ConvertToString(key);
            return string.IsNullOrWhiteSpace(text) ? $"Key {virtualKey}" : text;
        }
        catch
        {
            return $"Key {virtualKey}";
        }
    }
}
