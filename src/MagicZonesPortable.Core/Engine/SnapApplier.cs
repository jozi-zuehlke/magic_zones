namespace MagicZonesPortable.Core.Engine;

using MagicZonesPortable.Core.Abstractions;
using MagicZonesPortable.Core.Logging;

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
        _logger.Debug(
            $"Applying snap to window 0x{hwnd:X} with target bounds " +
            $"({zoneBounds.X}, {zoneBounds.Y}, {zoneBounds.Width}, {zoneBounds.Height}).");

        if (_windowManager.IsZoomed(hwnd))
        {
            _logger.Debug($"Window 0x{hwnd:X} is maximized; restoring before snap.");
            _windowManager.ShowWindow(hwnd, SW_RESTORE);
        }

        var compensated = ComputeDwmCompensatedBounds(hwnd, zoneBounds);
        _logger.Debug(
            $"Using final window bounds ({compensated.X}, {compensated.Y}, {compensated.Width}, {compensated.Height}) after DWM compensation.");

        _windowManager.SetWindowPos(hwnd, compensated.X, compensated.Y, compensated.Width, compensated.Height);

        _logger.Info($"Snapped window 0x{hwnd:X} to zone ({zoneBounds.X}, {zoneBounds.Y}, {zoneBounds.Width}, {zoneBounds.Height})");
    }

    /// <summary>
    /// Measures DWM frame insets at the window's current position and applies them
    /// to the target zone bounds, so only a single SetWindowPos call is needed.
    /// </summary>
    private Rectangle ComputeDwmCompensatedBounds(nint hwnd, Rectangle zoneBounds)
    {
        var extBounds = _windowManager.GetExtendedFrameBounds(hwnd);
        if (extBounds is null)
        {
            _logger.Debug($"No extended frame bounds available for window 0x{hwnd:X}; skipping DWM compensation.");
            return zoneBounds;
        }

        var windowRect = _windowManager.GetWindowRect(hwnd);
        var ext = extBounds.Value;

        int leftInset = ext.X - windowRect.X;
        int topInset = ext.Y - windowRect.Y;
        int rightInset = (windowRect.X + windowRect.Width) - (ext.X + ext.Width);
        int bottomInset = (windowRect.Y + windowRect.Height) - (ext.Y + ext.Height);

        if (leftInset == 0 && topInset == 0 && rightInset == 0 && bottomInset == 0)
        {
            _logger.Debug($"Window 0x{hwnd:X} has zero DWM frame insets; no compensation required.");
            return zoneBounds;
        }

        _logger.Info($"DWM frame compensation: insets L={leftInset} T={topInset} R={rightInset} B={bottomInset}");

        return new Rectangle(
            zoneBounds.X - leftInset,
            zoneBounds.Y - topInset,
            zoneBounds.Width + leftInset + rightInset,
            zoneBounds.Height + topInset + bottomInset);
    }
}
