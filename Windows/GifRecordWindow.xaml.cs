using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Clipper.Native;
using Clipper.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Clipper.Windows;

public partial class GifRecordWindow : Window
{
    private readonly Drawing.Rectangle _regionPx;
    private readonly TimeSpan _max;
    private Drawing.Rectangle _hudPx;
    private bool _stopped;

    public event Action? StopRequested;

    public bool OverlapsRegion { get; private set; }

    public GifRecordWindow(Drawing.Rectangle regionPx, TimeSpan max)
    {
        InitializeComponent();
        _regionPx = regionPx;
        _max = max;
        TimerText.Text = $"0:00 / {Format(_max)}";
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        KeyDown += OnKeyDown;
    }

    public void SetElapsed(TimeSpan elapsed)
    {
        var shown = elapsed > _max ? _max : elapsed;
        TimerText.Text = $"{Format(shown)} / {Format(_max)}";
    }

    public void HideForCapture()
    {
        if (OverlapsRegion)
        {
            Opacity = 0;
        }
    }

    public void ShowAfterCapture()
    {
        if (OverlapsRegion)
        {
            Opacity = 1;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => PlaceNearRegion();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceNearRegion();
        Activate();
        Focus();
        Keyboard.Focus(this);

        var pulse = new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(520))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        RecDot.BeginAnimation(OpacityProperty, pulse);
    }

    private void PlaceNearRegion()
    {
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DesiredSize;
        var source = PresentationSource.FromVisual(this);
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var pixelW = Math.Max(1, (int)Math.Ceiling(size.Width * toDevice.M11));
        var pixelH = Math.Max(1, (int)Math.Ceiling(size.Height * toDevice.M22));

        var virtualScreen = Forms.SystemInformation.VirtualScreen;
        var gap = 10;
        var x = _regionPx.X + Math.Max(0, (_regionPx.Width - pixelW) / 2);
        var y = _regionPx.Y - pixelH - gap;
        if (y < virtualScreen.Top)
        {
            y = _regionPx.Y + _regionPx.Height + gap;
        }

        if (y + pixelH > virtualScreen.Bottom)
        {
            y = Math.Max(virtualScreen.Top, _regionPx.Y + gap);
        }

        x = Math.Clamp(x, virtualScreen.Left, Math.Max(virtualScreen.Left, virtualScreen.Right - pixelW));
        y = Math.Clamp(y, virtualScreen.Top, Math.Max(virtualScreen.Top, virtualScreen.Bottom - pixelH));

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HwndTopMost,
            x,
            y,
            pixelW,
            pixelH,
            NativeMethods.SwpShowWindow);
        Topmost = true;

        _hudPx = new Drawing.Rectangle(x, y, pixelW, pixelH);
        OverlapsRegion = _hudPx.IntersectsWith(_regionPx);
    }

    private void OnStop(object sender, RoutedEventArgs e) => RequestStop();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestStop();
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestStop();
            e.Handled = true;
        }
    }

    private void RequestStop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        StopRequested?.Invoke();
    }

    private static string Format(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
