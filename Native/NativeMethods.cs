using System.Runtime.InteropServices;
using System.Text;

namespace Clipper.Native;

internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModShift = 0x0004;
    public const int ModWin = 0x0008;
    public const int ModNoRepeat = 0x4000;

    public static readonly IntPtr HwndMessage = new(-3);
    public static readonly IntPtr HwndTopMost = new(-1);

    public const uint SwpShowWindow = 0x0040;
    public const uint SwpNoActivate = 0x0010;

    public const int GwlStyle = -16;
    public const int GwlExStyle = -20;
    public const int WsSysMenu = 0x00080000;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExAppWindow = 0x00040000;
    public const int WsExTopMost = 0x00000008;

    public const int WmNcHitTest = 0x0084;
    public const int WmNcLButtonDown = 0x00A1;
    public const int WmNcLButtonUp = 0x00A2;
    public const int WmNcLButtonDblClk = 0x00A3;
    public const int HtClient = 1;
    public const int HtMinButton = 8;
    public const int HtMaxButton = 9;
    public const int HtClose = 20;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);
    public const int DwmwaCloaked = 14;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    public const int DwmwaUseImmersiveDarkMode = 20;
    public const int DwmwaWindowCornerPreference = 33;
    public const int DwmwaBorderColor = 34;
    public const int DwmwaCaptionColor = 35;
    public const int DwmwcpRound = 2;
    public const int DwmBorderColor = 0x00413024; // #243041 as COLORREF (BBGGRR)
    public const int DwmCaptionColor = 0x00100B08; // #080B10 as COLORREF (BBGGRR)

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public const int WhKeyboardLl = 13;
    public const int WmKeyDown = 0x0100;
    public const int WmSysKeyDown = 0x0104;
    public const int WmKeyUp = 0x0101;
    public const int WmSysKeyUp = 0x0105;
    public const int VkShift = 0x10;
    public const int VkControl = 0x11;
    public const int VkMenu = 0x12;
    public const int VkLshift = 0xA0;
    public const int VkRshift = 0xA1;
    public const int VkLcontrol = 0xA2;
    public const int VkRcontrol = 0xA3;
    public const int VkLmenu = 0xA4;
    public const int VkRmenu = 0xA5;
    public const int VkLwin = 0x5B;
    public const int VkRwin = 0x5C;
    public const int WsPopup = unchecked((int)0x80000000);

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
}
