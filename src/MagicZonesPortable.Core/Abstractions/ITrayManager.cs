namespace MagicZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for system tray (notification area) icon management.
/// </summary>
public interface ITrayManager : IDisposable
{
    /// <summary>
    /// Shows the tray icon.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the tray icon.
    /// </summary>
    void Hide();

    /// <summary>
    /// Shows a balloon notification.
    /// </summary>
    void ShowBalloon(string title, string message, int timeoutMs = 3000);

    /// <summary>
    /// Updates the tray icon tooltip text.
    /// </summary>
    void SetTooltip(string text);

    /// <summary>
    /// Adds a context menu item to the tray icon.
    /// </summary>
    void AddMenuItem(string text, Action onClick);

    /// <summary>
    /// Fired when the tray icon is double-clicked.
    /// </summary>
    event EventHandler? DoubleClicked;
}
