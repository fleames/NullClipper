using System.Runtime.InteropServices;
using Clipper.Models;
using Clipper.Native;
using Forms = System.Windows.Forms;

namespace Clipper.Services;

public sealed class HotkeyService : IDisposable
{
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private HotkeyForm? _form;
    private Exception? _startError;

    public event Action? Pressed;

    public bool IsReady => _form is { IsHandleCreated: true };

    public HotkeyService()
    {
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _thread = new Thread(HotkeyThread)
        {
            IsBackground = true,
            Name = "NullClipperHotkey"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(3000) && _startError is not null)
        {
            throw _startError;
        }
    }

    public bool TryRegister(HotkeyBinding binding)
    {
        if (_form is null)
        {
            return false;
        }

        bool Register() => _form.Register(binding);
        return _form.InvokeRequired ? (bool)_form.Invoke(Register) : Register();
    }

    public void Pause()
    {
        if (_form is null)
        {
            return;
        }

        void Stop() => _form.SetListening(false);
        if (_form.InvokeRequired)
        {
            _form.Invoke(Stop);
        }
        else
        {
            Stop();
        }
    }

    public void Resume(HotkeyBinding binding) => TryRegister(binding);

    private void HotkeyThread()
    {
        try
        {
            Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
            _form = new HotkeyForm(
                () => _dispatcher.BeginInvoke(() => Pressed?.Invoke()),
                () => _ready.Set());
            Forms.Application.Run(_form);
        }
        catch (Exception ex)
        {
            _startError = ex;
            _ready.Set();
        }
    }

    public void Dispose()
    {
        try
        {
            if (_form is { IsHandleCreated: true })
            {
                _form.BeginInvoke(() =>
                {
                    _form.Unregister();
                    Forms.Application.ExitThread();
                });
            }
        }
        catch
        {
            // Form already torn down.
        }

        if (!_thread.Join(1000))
        {
            _form?.DestroyHandleSafe();
        }

        _ready.Dispose();
    }

    private sealed class HotkeyForm : Forms.Form
    {
        private const int HotkeyId = 1;
        private readonly Action _raise;
        private readonly Action _ready;
        private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
        private IntPtr _hook;
        private HotkeyBinding _binding = HotkeyBinding.Default;
        private bool _registered;
        private bool _listening = true;
        private bool _ctrl;
        private bool _shift;
        private bool _alt;
        private bool _win;
        private bool _keyHeld;
        private DateTime _lastRaise = DateTime.MinValue;

        public HotkeyForm(Action raise, Action ready)
        {
            _raise = raise;
            _ready = ready;
            _hookProc = HookProc;
            FormBorderStyle = Forms.FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = Forms.FormStartPosition.Manual;
            Location = new System.Drawing.Point(-32000, -32000);
            Size = new System.Drawing.Size(1, 1);
            Text = "NullClipperHotkey";
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated)
            {
                CreateHandle();
            }

            base.SetVisibleCore(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var module = NativeMethods.GetModuleHandle("user32.dll");
            _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _hookProc, module, 0);
            _ready();
        }

        public bool Register(HotkeyBinding binding)
        {
            Unregister();
            _binding = binding;
            _keyHeld = false;
            _listening = true;
            var modifiers = binding.Modifiers | (uint)NativeMethods.ModNoRepeat;
            _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers, binding.VirtualKey);
            if (!_registered)
            {
                _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, binding.Modifiers, binding.VirtualKey);
            }

            return _registered || _hook != IntPtr.Zero;
        }

        public void SetListening(bool listening)
        {
            _listening = listening;
            _keyHeld = false;
            if (!listening)
            {
                Unregister();
            }
        }

        public void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        protected override void WndProc(ref Forms.Message m)
        {
            if (_listening && m.Msg == NativeMethods.WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                Raise();
            }

            base.WndProc(ref m);
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    var msg = wParam.ToInt32();
                    var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
                    var isDown = msg is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
                    var isUp = msg is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;

                    if (isDown || isUp)
                    {
                        ApplyModifier(info.VkCode, isDown);
                    }

                    var trigger = info.VkCode == _binding.VirtualKey;
                    if (isUp && trigger)
                    {
                        _keyHeld = false;
                    }

                    if (_listening && isDown && trigger && ModifiersMatch() && !_keyHeld)
                    {
                        _keyHeld = true;
                        Raise();
                        return (IntPtr)1;
                    }
                }
            }
            catch
            {
                // Never break the keyboard hook chain.
            }

            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void ApplyModifier(uint vk, bool down)
        {
            switch (vk)
            {
                case NativeMethods.VkControl:
                case NativeMethods.VkLcontrol:
                case NativeMethods.VkRcontrol:
                    _ctrl = down;
                    break;
                case NativeMethods.VkShift:
                case NativeMethods.VkLshift:
                case NativeMethods.VkRshift:
                    _shift = down;
                    break;
                case NativeMethods.VkMenu:
                case NativeMethods.VkLmenu:
                case NativeMethods.VkRmenu:
                    _alt = down;
                    break;
                case NativeMethods.VkLwin:
                case NativeMethods.VkRwin:
                    _win = down;
                    break;
            }
        }

        private bool ModifiersMatch()
        {
            var needCtrl = (_binding.Modifiers & NativeMethods.ModControl) != 0;
            var needShift = (_binding.Modifiers & NativeMethods.ModShift) != 0;
            var needAlt = (_binding.Modifiers & NativeMethods.ModAlt) != 0;
            var needWin = (_binding.Modifiers & NativeMethods.ModWin) != 0;
            return _ctrl == needCtrl && _shift == needShift && _alt == needAlt && _win == needWin;
        }

        private void Raise()
        {
            var now = DateTime.UtcNow;
            if (now - _lastRaise < TimeSpan.FromMilliseconds(500))
            {
                return;
            }

            _lastRaise = now;
            _raise();
        }

        protected override void OnFormClosed(Forms.FormClosedEventArgs e)
        {
            Unregister();
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            base.OnFormClosed(e);
        }

        public void DestroyHandleSafe()
        {
            try
            {
                if (IsHandleCreated)
                {
                    DestroyHandle();
                }
            }
            catch
            {
                // Already destroyed.
            }
        }
    }
}
