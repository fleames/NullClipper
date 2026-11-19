using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Clipper.Services;

public sealed class TrayIconService : IDisposable
{
    private const int RecentSnipCount = 8;

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly ContextMenuStrip _menu;

    public event Action? CaptureRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action<string>? RecentSnipRequested;
    public event Action<int>? DelaySelected;

    /// <summary>The delay the menu should show as checked. Set by App once settings load.</summary>
    public Func<int>? CurrentDelaySeconds { get; set; }

    /// <summary>Set by App when a background update check finds a newer release.</summary>
    public string? PendingUpdateTag { get; set; }

    public event Action? UpdateClicked;

    public TrayIconService()
    {
        _icon = AppIcon.LoadWinFormsIcon();
        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "NullClipper — click to capture",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                CaptureRequested?.Invoke();
            }
        };

        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            if (PendingUpdateTag is not null)
            {
                UpdateClicked?.Invoke();
            }
        };
    }

    public void ShowWelcome(string hotkeyLabel)
    {
        _notifyIcon.ShowBalloonTip(
            4000,
            "NullClipper is ready",
            $"Press {hotkeyLabel} to snip. The image is copied to the clipboard.",
            ToolTipIcon.Info);
    }

    public void ShowMessage(string title, string text)
    {
        _notifyIcon.ShowBalloonTip(2500, title, text, ToolTipIcon.Info);
    }

    public void ShowUpdateAvailable(string tag)
    {
        PendingUpdateTag = tag;
        _notifyIcon.ShowBalloonTip(
            8000,
            $"NullClipper {tag} is available",
            "Click to open the download page.",
            ToolTipIcon.Info);
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        _menu.Items.Add("Capture region", null, (_, _) => CaptureRequested?.Invoke());

        var delay = CurrentDelaySeconds?.Invoke() ?? 0;
        var delayMenu = new ToolStripMenuItem("Capture with delay");
        foreach (var (label, seconds) in new[] { ("Off", 0), ("3 seconds", 3), ("5 seconds", 5), ("10 seconds", 10) })
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = delay == seconds,
                CheckOnClick = false
            };
            item.Click += (_, _) => DelaySelected?.Invoke(seconds);
            delayMenu.DropDownItems.Add(item);
        }

        _menu.Items.Add(delayMenu);

        var recent = SnipHistoryService.Recent(RecentSnipCount);
        var recentMenu = new ToolStripMenuItem("Recent snips")
        {
            Enabled = recent.Count > 0
        };

        foreach (var file in recent)
        {
            var label = $"{file.LastWriteTime:MMM d HH:mm} · {file.Extension.TrimStart('.').ToUpperInvariant()}";
            var path = file.FullName;
            recentMenu.DropDownItems.Add(label, null, (_, _) => RecentSnipRequested?.Invoke(path));
        }

        _menu.Items.Add(recentMenu);
        _menu.Items.Add("Open snips folder", null, (_, _) => SnipHistoryService.OpenFolder());

        if (PendingUpdateTag is not null)
        {
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add($"Update to {PendingUpdateTag}…", null, (_, _) => UpdateClicked?.Invoke());
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke());
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}

public static class AppIcon
{
    public static string IcoPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "clipper.ico");

    public static string PngPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "clipper-icon.png");

    public static Icon LoadWinFormsIcon()
    {
        if (File.Exists(IcoPath))
        {
            return new Icon(IcoPath, 32, 32);
        }

        if (File.Exists(PngPath))
        {
            using var bitmap = new Bitmap(PngPath);
            return Icon.FromHandle(bitmap.GetHicon());
        }

        return SystemIcons.Application;
    }
}
