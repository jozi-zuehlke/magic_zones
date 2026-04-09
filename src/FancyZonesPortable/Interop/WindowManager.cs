using FancyZonesPortable.Core.Abstractions;
using Rectangle = FancyZonesPortable.Core.Abstractions.Rectangle;

namespace FancyZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IWindowManager"/> using Win32 P/Invoke calls.
/// </summary>
internal class WindowManager : IWindowManager
{
    public void SetWindowPos(nint hwnd, int x, int y, int width, int height)
    {
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    public void ShowWindow(nint hwnd, int command)
    {
        NativeMethods.ShowWindow(hwnd, command);
    }

    public bool IsZoomed(nint hwnd)
    {
        return NativeMethods.IsZoomed(hwnd);
    }

    public string GetWindowClassName(nint hwnd)
    {
        var buf = new char[256];
        int length = NativeMethods.GetClassNameW(hwnd, buf, 256);
        return new string(buf, 0, length);
    }

    public int GetWindowExStyle(nint hwnd)
    {
        return NativeMethods.GetWindowLongA(hwnd, NativeMethods.GWL_EXSTYLE);
    }

    public Rectangle GetWindowRect(nint hwnd)
    {
        NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect);
        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public nint GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    public Rectangle? GetExtendedFrameBounds(nint hwnd)
    {
        int hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT rect,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>());

        if (hr != 0)
            return null;

        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
