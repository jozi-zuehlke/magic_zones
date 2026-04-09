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

        _logger.Info($"Snapped window 0x{hwnd:X} to zone ({zoneBounds.X}, {zoneBounds.Y}, {zoneBounds.Width}, {zoneBounds.Height})");
    }
}
