using System.Text;
using Clipper.Native;
using Drawing = System.Drawing;

namespace Clipper.Services;

public static class WindowEnumerator
{
    private static readonly HashSet<string> SkipClasses =
    [
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow"
    ];

    public static List<Drawing.Rectangle> GetVisibleWindows()
    {
        var windows = new List<Drawing.Rectangle>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd) || IsCloaked(hwnd))
            {
                return true;
            }

            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
            if ((exStyle & NativeMethods.WsExToolWindow) != 0 &&
                (exStyle & NativeMethods.WsExAppWindow) == 0)
            {
                return true;
            }

            var className = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, className, className.Capacity);
            if (SkipClasses.Contains(className.ToString()))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            var bounds = Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (bounds.Width < 16 || bounds.Height < 16)
            {
                return true;
            }

            windows.Add(bounds);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        return NativeMethods.DwmGetWindowAttribute(
                   hwnd,
                   NativeMethods.DwmwaCloaked,
                   out var cloaked,
                   sizeof(int)) == 0
               && cloaked != 0;
    }
}
