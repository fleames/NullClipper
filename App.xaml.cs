using System.Windows;
using Clipper.Models;
using Clipper.Services;
using Clipper.Windows;

namespace Clipper;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\NullClipper.SingleInstance";
    private const string CaptureEventName = @"Local\NullClipper.TriggerCapture";

    private Mutex? _mutex;
    private EventWaitHandle? _captureEvent;
    private CancellationTokenSource? _signalLoop;
    private SettingsStore? _settings;
    private HotkeyService? _hotkeys;
    private TrayIconService? _tray;
    private CaptureCoordinator? _capture;
    private SettingsWindow? _settingsWindow;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryTakeSingleInstance())
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _captureEvent = new EventWaitHandle(false, EventResetMode.AutoReset, CaptureEventName);

        _settings = new SettingsStore();
        _settings.Load();
        _settings.Current.Normalize();
        _settings.Current.StartWithWindows = StartupService.IsEnabled();
        _settings.Save(_settings.Current);

        _capture = new CaptureCoordinator(_settings);
        _hotkeys = new HotkeyService();
        _hotkeys.Pressed += () => Dispatcher.BeginInvoke(() => _capture.Start());

        _tray = new TrayIconService();
        _tray.CaptureRequested += () => Dispatcher.BeginInvoke(() => _capture.Start());
        _tray.SettingsRequested += () => Dispatcher.BeginInvoke(OpenSettings);
        _tray.ExitRequested += () => Dispatcher.BeginInvoke(() => Shutdown());

        ApplyHotkey(showError: true);

        _signalLoop = new CancellationTokenSource();
        _ = ListenForSecondInstanceAsync(_signalLoop.Token);

        _tray.ShowWelcome(_settings.Current.Hotkey.Label);

        if (e.Args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)))
        {
            OpenSettings();
        }
    }

    private bool TryTakeSingleInstance()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out var created);
            if (!created)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            _ownsMutex = true;
            return true;
        }
        catch (AbandonedMutexException ex)
        {
            _mutex = ex.Mutex ?? new Mutex(true, MutexName, out _);
            _ownsMutex = true;
            return true;
        }
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(CaptureEventName);
            existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            using var created = new EventWaitHandle(false, EventResetMode.AutoReset, CaptureEventName);
            created.Set();
        }
    }

    private async Task ListenForSecondInstanceAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _captureEvent is not null)
            {
                await WaitHandleAsync(_captureEvent, token);
                if (!token.IsCancellationRequested)
                {
                    _ = Dispatcher.BeginInvoke(() => _capture?.Start());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App is shutting down.
        }
    }

    private static Task WaitHandleAsync(WaitHandle handle, CancellationToken token)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registered = ThreadPool.RegisterWaitForSingleObject(
            handle,
            (_, _) => tcs.TrySetResult(),
            null,
            -1,
            executeOnlyOnce: true);

        token.Register(() =>
        {
            registered.Unregister(null);
            tcs.TrySetCanceled(token);
        });

        return tcs.Task.ContinueWith(
            _ => registered.Unregister(null),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ApplyHotkey(bool showError = true)
    {
        if (_hotkeys is null || _settings is null)
        {
            return;
        }

        var binding = _settings.Current.Hotkey;
        var ok = _hotkeys.TryRegister(binding);
        if (!ok && showError)
        {
            _tray?.ShowMessage("NullClipper", $"Could not register {binding.Label}. Click the hotkey field and try another shortcut.");
        }
    }

    private void OpenSettings()
    {
        if (_settings is null || _capture is null || _hotkeys is null)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Reveal();
            return;
        }

        _settingsWindow = new SettingsWindow(
            _settings,
            () => _capture.Start(),
            () => ApplyHotkey(),
            () => _hotkeys.Pause(),
            Shutdown);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _signalLoop?.Cancel();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned.
            }
        }

        _mutex?.Dispose();
        _captureEvent?.Dispose();
        base.OnExit(e);
    }
}
