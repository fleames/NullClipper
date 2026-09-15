using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace Clipper.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;

    public event Action? CaptureRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _icon = AppIcon.LoadWinFormsIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "NullClipper — click to capture",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                CaptureRequested?.Invoke();
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

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Capture region", null, (_, _) => CaptureRequested?.Invoke());
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
        return menu;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
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
