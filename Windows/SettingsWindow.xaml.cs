using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clipper.Models;
using Clipper.Native;
using Clipper.Services;

namespace Clipper.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly Action _capture;
    private readonly Action _applyHotkey;
    private readonly Action _pauseHotkey;
    private readonly Action _quit;
    private bool _recording;

    public SettingsWindow(
        SettingsStore store,
        Action capture,
        Action applyHotkey,
        Action pauseHotkey,
        Action quit)
    {
        InitializeComponent();
        _store = store;
        _capture = capture;
        _applyHotkey = applyHotkey;
        _pauseHotkey = pauseHotkey;
        _quit = quit;

        ShowHotkey(store.Current.Hotkey.Label);
        ApplyCaptureMode(store.Current.CaptureMode, save: false);
        StartupBox.IsChecked = store.Current.StartWithWindows;

        NullImageEnabledBox.IsChecked = store.Current.NullImageEnabled;
        SelectExpiry(store.Current.NullImageExpiry);
        NullImageBurnBox.IsChecked = store.Current.NullImageBurnAfterView;
        NullImagePasswordBox.Password = store.Current.NullImagePassword ?? string.Empty;
        UpdatePasswordWatermark();
        UpdateNullImageOptions();

        PreviewKeyDown += OnWindowPreviewKeyDown;
        StateChanged += OnWindowStateChanged;
        Deactivated += (_, _) => CancelRecording();
        Closed += (_, _) => CancelRecording();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlExStyle, ex | NativeMethods.WsExToolWindow);
        var dark = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var round = NativeMethods.DwmwcpRound;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaWindowCornerPreference, ref round, sizeof(int));
        var border = NativeMethods.DwmBorderColor;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaBorderColor, ref border, sizeof(int));
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => HideToTray();

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    internal void HideToTray()
    {
        CancelRecording();
        WindowState = WindowState.Normal;
        Hide();
        Visibility = Visibility.Collapsed;
    }

    internal void Reveal()
    {
        ApplyCaptureMode(_store.Current.CaptureMode, save: false);
        Visibility = Visibility.Visible;
        WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void OnCaptureNow(object sender, RoutedEventArgs e)
    {
        HideToTray();
        _capture();
    }

    private void OnSnipMode(object sender, RoutedEventArgs e) => ApplyCaptureMode("snip", save: true);

    private void OnGifMode(object sender, RoutedEventArgs e) => ApplyCaptureMode("gif", save: true);

    private void ApplyCaptureMode(string mode, bool save)
    {
        var gif = string.Equals(mode, "gif", StringComparison.OrdinalIgnoreCase);
        SnipModeButton.IsChecked = !gif;
        GifModeButton.IsChecked = gif;
        CaptureTitle.Text = gif ? "Record GIF" : "Capture";
        CaptureSubtitle.Text = gif
            ? "Select a region, then Stop or wait 8s"
            : "Region, window, or full screen";
        CaptureModeHint.Text = gif
            ? "GIF records the selected rectangle at 12 fps for up to 8s. Longest side is capped at 640px so files stay small."
            : "Snip copies a still image. Switch to GIF to record a short clip of the same region.";
        if (save)
        {
            var settings = _store.Current;
            settings.CaptureMode = gif ? "gif" : "snip";
            _store.Save(settings);
        }
    }

    private void ShowHotkey(string label)
    {
        HotkeyRecorder.Tag = null;
        RecordingDot.Visibility = Visibility.Collapsed;
        HotkeyChips.Children.Clear();
        foreach (var part in label.Split(" + ", StringSplitOptions.RemoveEmptyEntries))
        {
            HotkeyChips.Children.Add(CreateKeycap(part));
        }
    }

    private void ShowHotkeyRecording()
    {
        HotkeyRecorder.Tag = "rec";
        RecordingDot.Visibility = Visibility.Visible;
        HotkeyChips.Children.Clear();
        HotkeyChips.Children.Add(new TextBlock
        {
            Text = "Press a shortcut…",
            FontFamily = (FontFamily)FindResource("MonoFont"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("MutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    private Border CreateKeycap(string text)
    {
        return new Border
        {
            Background = (Brush)FindResource("KeycapBgBrush"),
            BorderBrush = (Brush)FindResource("EdgeHoverBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0),
            MinWidth = 28,
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = text,
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private void OnHotkeyClick(object sender, MouseButtonEventArgs e)
    {
        if (_recording)
        {
            return;
        }

        e.Handled = true;
        _recording = true;
        _pauseHotkey();
        ShowHotkeyRecording();
        HotkeyHint.Text = "Esc cancels without changing it.";
        HotkeyRecorder.Focus();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording)
        {
            if (e.Key == Key.Escape)
            {
                HideToTray();
                e.Handled = true;
            }

            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        if (key == Key.Escape)
        {
            CancelRecording();
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            return;
        }

        uint modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            modifiers |= NativeMethods.ModControl;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
            Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            modifiers |= NativeMethods.ModShift;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ||
            Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        {
            modifiers |= NativeMethods.ModAlt;
        }

        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
        {
            modifiers |= NativeMethods.ModWin;
        }

        var binding = new HotkeyBinding(modifiers, vk);
        var settings = _store.Current;
        settings.SetHotkey(binding);
        _store.Save(settings);
        _recording = false;
        ShowHotkey(binding.Label);
        HotkeyHint.Text = "Click, then press the keys you want.";
        _applyHotkey();
    }

    private void CancelRecording()
    {
        if (!_recording)
        {
            return;
        }

        _recording = false;
        ShowHotkey(_store.Current.Hotkey.Label);
        HotkeyHint.Text = "Click, then press the keys you want.";
        _applyHotkey();
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        var enabled = StartupBox.IsChecked == true;
        StartupService.SetEnabled(enabled);
        var settings = _store.Current;
        settings.StartWithWindows = enabled;
        _store.Save(settings);
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quit();
    }

    private void SelectExpiry(string value)
    {
        foreach (var obj in NullImageExpiryBox.Items)
        {
            if (obj is ComboBoxItem item && (string)item.Tag == value)
            {
                NullImageExpiryBox.SelectedItem = item;
                return;
            }
        }

        NullImageExpiryBox.SelectedIndex = 1; // "1 day"
    }

    private void OnNullImageEnabledToggled(object sender, RoutedEventArgs e)
    {
        var settings = _store.Current;
        settings.NullImageEnabled = NullImageEnabledBox.IsChecked == true;
        _store.Save(settings);
        UpdateNullImageOptions();
    }

    private void UpdateNullImageOptions()
    {
        NullImageOptions.Opacity = NullImageEnabledBox.IsChecked == true ? 1 : 0.45;
        NullImageOptions.IsEnabled = NullImageEnabledBox.IsChecked == true;
    }

    private void OnNullImageExpiryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NullImageExpiryBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var settings = _store.Current;
        settings.NullImageExpiry = (string)item.Tag;
        _store.Save(settings);
    }

    private void OnNullImageBurnToggled(object sender, RoutedEventArgs e)
    {
        var settings = _store.Current;
        settings.NullImageBurnAfterView = NullImageBurnBox.IsChecked == true;
        _store.Save(settings);
    }

    private void OnNullImagePasswordChanged(object sender, RoutedEventArgs e)
    {
        var settings = _store.Current;
        settings.NullImagePassword = string.IsNullOrEmpty(NullImagePasswordBox.Password) ? null : NullImagePasswordBox.Password;
        _store.Save(settings);
        UpdatePasswordWatermark();
    }

    private void UpdatePasswordWatermark()
    {
        PasswordWatermark.Visibility = string.IsNullOrEmpty(NullImagePasswordBox.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
