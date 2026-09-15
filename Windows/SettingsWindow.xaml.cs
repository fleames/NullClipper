using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        HotkeyButton.Content = store.Current.Hotkey.Label;
        StartupBox.IsChecked = store.Current.StartWithWindows;

        NullImageEnabledBox.IsChecked = store.Current.NullImageEnabled;
        SelectExpiry(store.Current.NullImageExpiry);
        NullImageBurnBox.IsChecked = store.Current.NullImageBurnAfterView;
        NullImagePasswordBox.Password = store.Current.NullImagePassword ?? string.Empty;

        PreviewKeyDown += OnWindowPreviewKeyDown;
        Deactivated += (_, _) => CancelRecording();
        Closed += (_, _) => CancelRecording();
    }

    private void OnCaptureNow(object sender, RoutedEventArgs e)
    {
        CancelRecording();
        Hide();
        _capture();
    }

    private void OnHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (_recording)
        {
            return;
        }

        _recording = true;
        _pauseHotkey();
        HotkeyButton.Content = "Press a shortcut…";
        HotkeyHint.Text = "Esc cancels without changing it.";
        HotkeyButton.Focus();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording)
        {
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
        HotkeyButton.Content = binding.Label;
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
        HotkeyButton.Content = _store.Current.Hotkey.Label;
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
    }
}
