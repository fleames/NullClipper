using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Clipper.Services.NullImage;
using Clipper.Windows;
using Forms = System.Windows.Forms;

namespace Clipper.Services;

public sealed class CaptureCoordinator
{
    private readonly SettingsStore _settings;
    private readonly List<OverlayWindow> _overlays = [];
    private CaptureSession? _session;
    private bool _busy;

    public CaptureCoordinator(SettingsStore settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        var completed = false;
        _session = new CaptureSession();

        try
        {
            _session.Windows.AddRange(WindowEnumerator.GetVisibleWindows());

            foreach (var screen in Forms.Screen.AllScreens)
            {
                var freeze = ScreenCaptureService.Capture(screen);
                _session.Frames.Add((screen.Bounds, freeze));
                var overlay = new OverlayWindow(screen, freeze, _session);
                overlay.RectCaptured += rect => Complete(() => ScreenCaptureService.CropFromScreens(_session!.Frames, rect));
                overlay.ImageCaptured += bitmap => Complete(() => bitmap);
                overlay.Cancelled += () => Complete(() => null);
                _overlays.Add(overlay);
            }

            if (_overlays.Count == 0)
            {
                _busy = false;
                _session = null;
                return;
            }

            foreach (var overlay in _overlays)
            {
                overlay.Show();
                overlay.Activate();
            }

            FocusOverlayUnderCursor();
        }
        catch (Exception ex)
        {
            Cancel();
            Forms.MessageBox.Show(
                $"NullClipper could not start a snip:\n{ex.Message}",
                "NullClipper",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }

        void Complete(Func<Bitmap?> factory)
        {
            var overlayDispatcher = _overlays.FirstOrDefault()?.Dispatcher ?? Application.Current.Dispatcher;
            overlayDispatcher.BeginInvoke(() =>
            {
                if (completed)
                {
                    try
                    {
                        factory()?.Dispose();
                    }
                    catch
                    {
                        // Already finishing another snip.
                    }

                    return;
                }

                completed = true;
                Bitmap? capture = null;
                try
                {
                    capture = factory();
                }
                catch
                {
                    capture = null;
                }

                Finish(capture);
            });
        }
    }

    public void Cancel()
    {
        if (!_busy)
        {
            return;
        }

        Finish(null);
    }

    private void Finish(Bitmap? capture)
    {
        var overlays = _overlays.ToArray();
        _overlays.Clear();
        _session = null;

        foreach (var overlay in overlays)
        {
            try
            {
                overlay.Hide();
                overlay.Close();
            }
            catch
            {
                // Overlay may already be closing.
            }
        }

        _busy = false;

        if (capture is null)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(
            () => _ = FinishAsync(capture),
            DispatcherPriority.ApplicationIdle);
    }

    private async Task FinishAsync(Bitmap capture)
    {
        try
        {
            var settings = _settings.Current;
            if (settings.NullImageEnabled)
            {
                await UploadAndNotifyAsync(capture);
            }
            else
            {
                CopyToClipboardAndNotify(capture);
            }
        }
        finally
        {
            capture.Dispose();
        }
    }

    private async Task UploadAndNotifyAsync(Bitmap capture)
    {
        var preview = ClipboardService.Preview(capture);
        ToastWindow.Show("Uploading to NullImage…", $"{capture.Width} × {capture.Height}", preview);
        try
        {
            var result = await NullImageUploader.UploadCaptureAsync(capture, _settings.Current);
            ClipboardService.CopyText(result.ShareUrl);
            ToastWindow.Show("Link copied to clipboard", result.ShareUrl, preview);
        }
        catch (Exception ex)
        {
            ToastWindow.Show("Upload failed — copied snip instead", ex.Message, preview);
            CopyToClipboardAndNotify(capture, showToast: false);
        }
    }

    private static void CopyToClipboardAndNotify(Bitmap capture, bool showToast = true)
    {
        try
        {
            ClipboardService.CopyImage(capture);
            if (showToast)
            {
                var preview = ClipboardService.Preview(capture);
                ToastWindow.Show("Snip saved to clipboard", $"{capture.Width} × {capture.Height}", preview);
            }
        }
        catch
        {
            ToastWindow.Show("Could not copy snip", "Clipboard is busy — try again");
        }
    }

    private void FocusOverlayUnderCursor()
    {
        var position = Forms.Control.MousePosition;
        var match = _overlays.FirstOrDefault(o => o.ScreenBounds.Contains(position))
                    ?? _overlays[0];
        match.Activate();
        match.Focus();
    }
}
