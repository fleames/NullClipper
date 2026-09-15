using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clipper.Native;
using Clipper.Services;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace Clipper.Windows;

public partial class OverlayWindow : Window
{
    private readonly Drawing.Bitmap _freeze;
    private readonly CaptureSession _session;
    private readonly List<System.Windows.Point> _freeform = [];
    private bool _selecting;
    private System.Windows.Point _start;
    private Rect _selection;
    private Drawing.Rectangle? _hoverWindow;
    private bool _closed;

    public event Action<Drawing.Rectangle>? RectCaptured;
    public event Action<Drawing.Bitmap>? ImageCaptured;
    public event Action? Cancelled;

    public Drawing.Rectangle ScreenBounds { get; }

    public OverlayWindow(Forms.Screen screen, Drawing.Bitmap freeze, CaptureSession session)
    {
        InitializeComponent();
        ScreenBounds = screen.Bounds;
        _freeze = freeze;
        _session = session;
        FrozenImage.Source = ScreenCaptureService.ToBitmapSource(freeze);
        DimPath.Data = new RectangleGeometry(new Rect(0, 0, screen.Bounds.Width, screen.Bounds.Height));

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        KeyDown += OnKeyDown;
        MouseEnter += (_, _) => Activate();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonUp += (_, _) => RequestCancel();
        SizeChanged += (_, _) => Redraw();
        _session.ModeChanged += OnModeChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => PositionToScreen();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionToScreen();
        Activate();
        Focus();
        Keyboard.Focus(this);
        UpdateModeButtons();
        Redraw();
    }

    private void PositionToScreen()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HwndTopMost,
            ScreenBounds.X,
            ScreenBounds.Y,
            ScreenBounds.Width,
            ScreenBounds.Height,
            NativeMethods.SwpShowWindow);
        Topmost = true;
        Visibility = Visibility.Visible;
        Opacity = 1;
    }

    private void OnModeChanged(SnipMode _)
    {
        ResetSelection();
        UpdateModeButtons();
        Cursor = _session.Mode == SnipMode.Window ? Cursors.Arrow : Cursors.Cross;
        Redraw();
    }

    private void OnRectangleMode(object sender, RoutedEventArgs e) => _session.SetMode(SnipMode.Rectangle);

    private void OnFreeformMode(object sender, RoutedEventArgs e) => _session.SetMode(SnipMode.Freeform);

    private void OnWindowMode(object sender, RoutedEventArgs e) => _session.SetMode(SnipMode.Window);

    private void OnFullScreen(object sender, RoutedEventArgs e) => CompleteRect(ScreenBounds);

    private void OnCloseClicked(object sender, RoutedEventArgs e) => RequestCancel();

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestCancel();
            e.Handled = true;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Toolbar.IsMouseOver)
        {
            return;
        }

        if (_session.Mode == SnipMode.Window)
        {
            if (_hoverWindow is { } window)
            {
                CompleteRect(window);
            }

            return;
        }

        _selecting = true;
        _start = e.GetPosition(this);
        _selection = new Rect(_start, new System.Windows.Size(0, 0));
        _freeform.Clear();
        _freeform.Add(_start);
        CaptureMouse();
        Redraw();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_session.Mode == SnipMode.Window)
        {
            UpdateWindowHover();
            Redraw();
            return;
        }

        if (!_selecting)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (_session.Mode == SnipMode.Freeform)
        {
            if (_freeform.Count == 0 || (current - _freeform[^1]).Length > 1.5)
            {
                _freeform.Add(current);
            }
        }
        else
        {
            _selection = new Rect(_start, current);
        }

        Redraw();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (_session.Mode == SnipMode.Freeform)
        {
            CompleteFreeform();
            return;
        }

        if (_selection.Width < 3 || _selection.Height < 3)
        {
            ResetSelection();
            Redraw();
            return;
        }

        CompleteRect(ToVirtualRect(_selection));
    }

    private void UpdateWindowHover()
    {
        var pointer = Forms.Control.MousePosition;
        _hoverWindow = _session.Windows.FirstOrDefault(window => window.Contains(pointer));
        if (_hoverWindow == default(Drawing.Rectangle))
        {
            _hoverWindow = null;
        }
    }

    private void CompleteFreeform()
    {
        var bitmap = BuildFreeformBitmap();
        if (bitmap is null)
        {
            ResetSelection();
            Redraw();
            return;
        }

        if (_closed)
        {
            bitmap.Dispose();
            return;
        }

        _closed = true;
        ImageCaptured?.Invoke(bitmap);
    }

    private Drawing.Bitmap? BuildFreeformBitmap()
    {
        if (_freeform.Count < 8)
        {
            return null;
        }

        var geo = new PathGeometry();
        var figure = new PathFigure { StartPoint = _freeform[0], IsClosed = true };
        figure.Segments.Add(new PolyLineSegment(_freeform.Skip(1), true));
        geo.Figures.Add(figure);
        var bounds = geo.Bounds;
        if (bounds.Width < 3 || bounds.Height < 3)
        {
            return null;
        }

        var cropped = ScreenCaptureService.Crop(_freeze, ToPixelRect(bounds));
        var points = _freeform
            .Select(point => new Drawing.PointF(
                (float)((point.X - bounds.X) * cropped.Width / Math.Max(1, bounds.Width)),
                (float)((point.Y - bounds.Y) * cropped.Height / Math.Max(1, bounds.Height))))
            .ToArray();

        var masked = new Drawing.Bitmap(cropped.Width, cropped.Height, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(masked))
        {
            graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Drawing.Color.Transparent);
            using var path = new Drawing2D.GraphicsPath();
            path.AddPolygon(points);
            graphics.SetClip(path);
            graphics.DrawImage(cropped, 0, 0);
        }

        cropped.Dispose();
        return masked;
    }

    private void CompleteRect(Drawing.Rectangle virtualRect)
    {
        if (_closed || virtualRect.Width < 2 || virtualRect.Height < 2)
        {
            return;
        }

        _closed = true;
        RectCaptured?.Invoke(virtualRect);
    }

    private void RequestCancel()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        Cancelled?.Invoke();
    }

    private Drawing.Rectangle ToVirtualRect(Rect selection)
    {
        var pixels = ToPixelRect(selection);
        return new Drawing.Rectangle(
            ScreenBounds.X + pixels.X,
            ScreenBounds.Y + pixels.Y,
            pixels.Width,
            pixels.Height);
    }

    private Int32Rect ToPixelRect(Rect selection)
    {
        var scaleX = _freeze.Width / Math.Max(1.0, ActualWidth);
        var scaleY = _freeze.Height / Math.Max(1.0, ActualHeight);
        var x = (int)Math.Floor(selection.X * scaleX);
        var y = (int)Math.Floor(selection.Y * scaleY);
        var width = (int)Math.Ceiling(selection.Width * scaleX);
        var height = (int)Math.Ceiling(selection.Height * scaleY);
        return new Int32Rect(x, y, Math.Max(1, width), Math.Max(1, height));
    }

    private Rect ToLocalRect(Drawing.Rectangle virtualRect)
    {
        var scaleX = ActualWidth / Math.Max(1.0, _freeze.Width);
        var scaleY = ActualHeight / Math.Max(1.0, _freeze.Height);
        return new Rect(
            (virtualRect.X - ScreenBounds.X) * scaleX,
            (virtualRect.Y - ScreenBounds.Y) * scaleY,
            virtualRect.Width * scaleX,
            virtualRect.Height * scaleY);
    }

    private void ResetSelection()
    {
        _selecting = false;
        _selection = default;
        _freeform.Clear();
        _hoverWindow = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void UpdateModeButtons()
    {
        Highlight(RectangleButton, _session.Mode == SnipMode.Rectangle);
        Highlight(FreeformButton, _session.Mode == SnipMode.Freeform);
        Highlight(WindowButton, _session.Mode == SnipMode.Window);
    }

    private static void Highlight(Button button, bool on)
    {
        button.Background = on
            ? new SolidColorBrush(Color.FromArgb(255, 59, 130, 246))
            : Brushes.Transparent;
    }

    private void Redraw()
    {
        SelectionBorder.Visibility = Visibility.Collapsed;
        WindowHighlight.Visibility = Visibility.Collapsed;
        FreeformLine.Visibility = Visibility.Collapsed;
        SizeBadge.Visibility = Visibility.Collapsed;

        var hole = (Geometry?)null;

        if (_session.Mode == SnipMode.Window && _hoverWindow is { } window)
        {
            var local = ToLocalRect(window);
            Canvas.SetLeft(WindowHighlight, local.X);
            Canvas.SetTop(WindowHighlight, local.Y);
            WindowHighlight.Width = Math.Max(0, local.Width);
            WindowHighlight.Height = Math.Max(0, local.Height);
            WindowHighlight.Visibility = Visibility.Visible;
            hole = new RectangleGeometry(local);
        }
        else if (_session.Mode == SnipMode.Freeform && _freeform.Count > 1)
        {
            FreeformLine.Points = new PointCollection(_freeform);
            FreeformLine.Visibility = Visibility.Visible;
            var geo = new PathGeometry();
            var figure = new PathFigure { StartPoint = _freeform[0], IsClosed = _selecting == false && _freeform.Count > 2 };
            figure.Segments.Add(new PolyLineSegment(_freeform.Skip(1), true));
            geo.Figures.Add(figure);
            hole = geo;
        }
        else if (_selection.Width >= 1 && _selection.Height >= 1)
        {
            Canvas.SetLeft(SelectionBorder, _selection.X);
            Canvas.SetTop(SelectionBorder, _selection.Y);
            SelectionBorder.Width = _selection.Width;
            SelectionBorder.Height = _selection.Height;
            SelectionBorder.Visibility = Visibility.Visible;
            hole = new RectangleGeometry(_selection);

            var pixels = ToPixelRect(_selection);
            SizeText.Text = $"{pixels.Width} × {pixels.Height}";
            SizeBadge.Visibility = Visibility.Visible;
            var badgeX = _selection.X;
            var badgeY = _selection.Y + _selection.Height + 8;
            if (badgeY + 32 > ActualHeight)
            {
                badgeY = Math.Max(8, _selection.Y - 32);
            }

            badgeX = Math.Clamp(badgeX, 8, Math.Max(8, ActualWidth - 120));
            SizeBadge.Margin = new Thickness(badgeX, badgeY, 0, 0);
        }

        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        DimPath.Data = hole is null
            ? full
            : new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _session.ModeChanged -= OnModeChanged;
        _freeze.Dispose();
    }
}
