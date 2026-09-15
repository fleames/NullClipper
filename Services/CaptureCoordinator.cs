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
        _session.SetKind(_settings.Current.IsGifMode ? CaptureKind.Gif : CaptureKind.Snip);
        _session.KindChanged += OnSessionKindChanged;

        try
        {
            _session.Windows.AddRange(WindowEnumerator.GetVisibleWindows());

            foreach (var screen in Forms.Screen.AllScreens)
            {
                var freeze = ScreenCaptureService.Capture(screen);
                _session.Frames.Add((screen.Bounds, freeze));
                var overlay = new OverlayWindow(screen, freeze, _session);
                overlay.RectCaptured += rect =>
                {
                    if (_session?.Kind == CaptureKind.Gif)
                    {
                        CompleteGif(rect);
                    }
                    else
                    {
                        Complete(() => ScreenCaptureService.CropFromScreens(_session!.Frames, rect));
                    }
                };
                overlay.ImageCaptured += bitmap =>
                {
                    if (_session?.Kind == CaptureKind.Gif)
                    {
                        bitmap.Dispose();
                        Complete(() => null);
                    }
                    else
                    {
                        Complete(() => bitmap);
                    }
                };
                overlay.Cancelled += () => Complete(() => null);
                _overlays.Add(overlay);
            }

            if (_overlays.Count == 0)
            {
                DetachSession();
                _busy = false;
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

        void CompleteGif(Rectangle rect)
        {
            var overlayDispatcher = _overlays.FirstOrDefault()?.Dispatcher ?? Application.Current.Dispatcher;
            overlayDispatcher.BeginInvoke(() =>
            {
                if (completed)
                {
                    return;
                }

                completed = true;
                CloseOverlays();
                _ = RecordAndFinishGifAsync(rect);
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

    private void OnSessionKindChanged(CaptureKind kind)
    {
        var settings = _settings.Current;
        settings.CaptureMode = kind == CaptureKind.Gif ? "gif" : "snip";
        _settings.Save(settings);
    }

    private void Finish(Bitmap? capture)
    {
        CloseOverlays();
        _busy = false;

        if (capture is null)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(
            () => _ = FinishAsync(capture),
            DispatcherPriority.ApplicationIdle);
    }

    private void CloseOverlays()
    {
        var overlays = _overlays.ToArray();
        _overlays.Clear();
        DetachSession();

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
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.KindChanged -= OnSessionKindChanged;
        _session = null;
    }

    private async Task RecordAndFinishGifAsync(Rectangle region)
    {
        GifRecordWindow? hud = null;
        using var cts = new CancellationTokenSource();
        try
        {
            await Task.Delay(90);
            hud = new GifRecordWindow(region, TimeSpan.FromSeconds(GifLimits.MaxDurationSeconds));
            hud.StopRequested += () => cts.Cancel();
            hud.Show();
            hud.Activate();

            var capturedHud = hud;
            var result = await GifCaptureService.RecordAsync(
                region,
                elapsed => capturedHud.Dispatcher.Invoke(() => capturedHud.SetElapsed(elapsed)),
                async () =>
                {
                    await capturedHud.Dispatcher.InvokeAsync(capturedHud.HideForCapture);
                    await capturedHud.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                },
                () => capturedHud.Dispatcher.InvokeAsync(capturedHud.ShowAfterCapture).Task,
                () => ToastWindow.Show("Encoding GIF…", "Quantizing frames", gif: true),
                cts.Token);

            hud.Close();
            hud = null;

            if (result is null)
            {
                return;
            }

            await FinishGifAsync(result);
        }
        catch (Exception ex)
        {
            ToastWindow.Show("Could not record GIF", ex.Message, gif: true);
        }
        finally
        {
            try
            {
                hud?.Close();
            }
            catch
            {
                // HUD may already be closed.
            }

            _busy = false;
        }
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

    private async Task FinishGifAsync(GifCaptureResult result)
    {
        try
        {
            var settings = _settings.Current;
            var preview = ClipboardService.Preview(result.Preview);
            if (settings.NullImageEnabled)
            {
                ToastWindow.Show("Uploading to NullImage…", $"{result.Width} × {result.Height} · {result.FrameCount} frames", preview, gif: true);
                try
                {
                    var upload = await NullImageUploader.UploadBytesAsync(
                        result.Bytes,
                        $"clip-{DateTime.Now:yyyyMMdd-HHmmss}.gif",
                        "image/gif",
                        settings);
                    ClipboardService.CopyText(upload.ShareUrl);
                    ToastWindow.Show("Link copied to clipboard", upload.ShareUrl, preview, gif: true);
                }
                catch (Exception ex)
                {
                    ToastWindow.Show("Upload failed — copied GIF instead", ex.Message, preview, gif: true);
                    ClipboardService.CopyGif(result.Bytes, result.Preview);
                }
            }
            else
            {
                ClipboardService.CopyGif(result.Bytes, result.Preview);
                ToastWindow.Show("GIF saved to clipboard", $"{result.Width} × {result.Height} · {result.FrameCount} frames", preview, gif: true);
            }
        }
        catch
        {
            ToastWindow.Show("Could not copy GIF", "Clipboard is busy — try again", gif: true);
        }
        finally
        {
            result.Preview.Dispose();
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
