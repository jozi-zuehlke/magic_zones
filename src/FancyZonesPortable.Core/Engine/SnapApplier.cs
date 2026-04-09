namespace FancyZonesPortable.Core.Engine;

using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Logging;

public class SnapApplier
{
    private readonly IWindowManager _windowManager;
    private readonly ILogger _logger;

    private const int SW_RESTORE = 9;

    public SnapApplier(IWindowManager windowManager, ILogger logger)
    {
        _windowManager = windowManager;
        _logger = logger;
    }

    public void Apply(nint hwnd, Rectangle zoneBounds)
    {
        if (_windowManager.IsZoomed(hwnd))
            _windowManager.ShowWindow(hwnd, SW_RESTORE);

        _windowManager.SetWindowPos(hwnd, zoneBounds.X, zoneBounds.Y, zoneBounds.Width, zoneBounds.Height);

        CompensateForDwmFrame(hwnd, zoneBounds);

        _logger.Info($"Snapped window 0x{hwnd:X} to zone ({zoneBounds.X}, {zoneBounds.Y}, {zoneBounds.Width}, {zoneBounds.Height})");
    }

    private void CompensateForDwmFrame(nint hwnd, Rectangle zoneBounds)
    {
        var extBounds = _windowManager.GetExtendedFrameBounds(hwnd);
        if (extBounds is null)
            return;

        var windowRect = _windowManager.GetWindowRect(hwnd);
        var ext = extBounds.Value;

        int leftInset = ext.X - windowRect.X;
        int topInset = ext.Y - windowRect.Y;
        int rightInset = (windowRect.X + windowRect.Width) - (ext.X + ext.Width);
        int bottomInset = (windowRect.Y + windowRect.Height) - (ext.Y + ext.Height);

        if (leftInset == 0 && topInset == 0 && rightInset == 0 && bottomInset == 0)
            return;

        _windowManager.SetWindowPos(hwnd,
            zoneBounds.X - leftInset,
            zoneBounds.Y - topInset,
            zoneBounds.Width + leftInset + rightInset,
            zoneBounds.Height + topInset + bottomInset);

        _logger.Info($"DWM frame compensation: insets L={leftInset} T={topInset} R={rightInset} B={bottomInset}");
    }
}
