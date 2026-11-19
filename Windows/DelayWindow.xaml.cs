using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Clipper.Native;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Clipper.Windows;

/// <summary>
/// The "capture in N…" countdown. Own window rather than a toast so it can take
/// focus and swallow Esc, and so the user can see it tick while the menus and
/// tooltips they are trying to catch stay open.
/// </summary>
public partial class DelayWindow : Window
{
    private bool _cancelled;

    public event Action? Cancelled;

    public DelayWindow(int seconds)
    {
        InitializeComponent();
        CountText.Text = seconds.ToString();
        SourceInitialized += (_, _) => PlaceNearCursor();
        Loaded += (_, _) =>
        {
            PlaceNearCursor();
            Activate();
            Focus();
            Keyboard.Focus(this);
        };
        PreviewKeyDown += OnPreviewKeyDown;
        MouseLeftButtonDown += (_, _) => RequestCancel();
    }

    public void SetRemaining(int seconds) => CountText.Text = Math.Max(0, seconds).ToString();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestCancel();
            e.Handled = true;
        }
    }

    private void RequestCancel()
    {
        if (_cancelled)
        {
            return;
        }

        _cancelled = true;
        Cancelled?.Invoke();
    }

    private void PlaceNearCursor()
    {
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DesiredSize;
        var source = PresentationSource.FromVisual(this);
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var pixelW = Math.Max(1, (int)Math.Ceiling(size.Width * toDevice.M11));
        var pixelH = Math.Max(1, (int)Math.Ceiling(size.Height * toDevice.M22));

        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor).Bounds;
        var x = cursor.X + 18;
        var y = cursor.Y + 18;

        // Flip above/left of the pointer rather than run off the monitor.
        if (x + pixelW > screen.Right)
        {
            x = cursor.X - pixelW - 18;
        }

        if (y + pixelH > screen.Bottom)
        {
            y = cursor.Y - pixelH - 18;
        }

        x = Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - pixelW));
        y = Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - pixelH));

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
    }
}
