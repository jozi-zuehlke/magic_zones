namespace FancyZonesPortable.Core.Engine;

using FancyZonesPortable.Core.Abstractions;

public class WindowFilter
{
    private readonly IWindowManager _windowManager;
    private nint _overlayHwnd;

    private const int WS_EX_TOOLWINDOW = 0x00000080;

    public WindowFilter(IWindowManager windowManager, nint overlayHwnd = 0)
    {
        _windowManager = windowManager;
        _overlayHwnd = overlayHwnd;
    }

    /// <summary>
    /// Updates the overlay window handle used for filtering.
    /// </summary>
    public void SetOverlayHandle(nint hwnd)
    {
        _overlayHwnd = hwnd;
    }

    /// <summary>
    /// Returns false for windows that should NOT be snapped
    /// (taskbar, tool windows, overlay, zero handle).
    /// </summary>
    public bool ShouldSnap(nint hwnd)
    {
        if (hwnd == 0)
            return false;

        if (hwnd == _overlayHwnd)
            return false;

        var className = _windowManager.GetWindowClassName(hwnd);
        if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;

        var exStyle = _windowManager.GetWindowExStyle(hwnd);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        return true;
    }
}
