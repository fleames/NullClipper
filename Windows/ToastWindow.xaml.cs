using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Clipper.Windows;

public partial class ToastWindow : Window
{
    private ToastWindow(string title, string? detail, ImageSource? preview)
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

        Opacity = 0;
        Loaded += OnLoaded;
    }

    public static void Show(string title, string? detail = null, BitmapSource? preview = null)
    {
        var toast = new ToastWindow(title, detail, preview);
        toast.Show();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var screen = Forms.Screen.FromPoint(Forms.Control.MousePosition);
        var working = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice;
        var pixelX = working.Left + (working.Width - ActualWidth) / 2.0;
        var pixelY = working.Bottom - ActualHeight - 28;
        if (transform is { } matrix)
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

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
        BeginAnimation(OpacityProperty, fadeIn);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            fadeOut.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        };
        timer.Start();
    }
}
