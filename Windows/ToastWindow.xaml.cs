using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Clipper.Windows;

public partial class ToastWindow : Window
{
    private static ToastWindow? _current;
    private readonly DispatcherTimer _dismiss;

    private ToastWindow(string title, string? detail, ImageSource? preview, bool gif)
    {
        InitializeComponent();
        TitleText.Text = title;
        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;
        if (preview is null)
        {
            PreviewHost.Visibility = Visibility.Collapsed;
        }
        else
        {
            PreviewImage.Source = preview;
        }

        GifBadge.Visibility = gif && preview is not null ? Visibility.Visible : Visibility.Collapsed;
        Opacity = 0;
        Loaded += OnLoaded;

        _dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
        _dismiss.Tick += (_, _) =>
        {
            _dismiss.Stop();
            Dismiss();
        };
    }

    public static void Show(string title, string? detail = null, BitmapSource? preview = null, bool gif = false)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Show(title, detail, preview, gif));
            return;
        }

        _current?.CloseQuietly();
        var toast = new ToastWindow(title, detail, preview, gif);
        _current = toast;
        toast.Closed += (_, _) =>
        {
            if (ReferenceEquals(_current, toast))
            {
                _current = null;
            }
        };
        toast.Show();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceAboveTaskbar();

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });

        _dismiss.Start();
    }

    private void PlaceAboveTaskbar()
    {
        var screen = Forms.Screen.FromPoint(Forms.Control.MousePosition);
        var working = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice;
        var pixelX = working.Left + (working.Width - ActualWidth) / 2.0;
        var pixelY = working.Bottom - ActualHeight - 36;
        if (fromDevice is { } matrix)
        {
            var dip = matrix.Transform(new System.Windows.Point(pixelX, pixelY));
            Left = dip.X;
            Top = dip.Y;
        }
        else
        {
            Left = pixelX;
            Top = pixelY;
        }
    }

    private void Dismiss()
    {
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
        fade.Completed += (_, _) => CloseQuietly();
        BeginAnimation(OpacityProperty, fade);
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(0, 12, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
    }

    private void CloseQuietly()
    {
        _dismiss.Stop();
        try
        {
            Close();
        }
        catch
        {
            // Already closing.
        }
    }
}
