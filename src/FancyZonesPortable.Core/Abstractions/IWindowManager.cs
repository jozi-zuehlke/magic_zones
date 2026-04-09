namespace FancyZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for Win32 window management operations.
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Sets the position, size, and Z-order of a window.
    /// </summary>
    void SetWindowPos(nint hwnd, int x, int y, int width, int height);

    /// <summary>
    /// Shows or hides a window.
    /// </summary>
    void ShowWindow(nint hwnd, int command);

    /// <summary>
    /// Returns true if the window is maximized.
    /// </summary>
    bool IsZoomed(nint hwnd);

    /// <summary>
    /// Gets the class name of a window.
    /// </summary>
    string GetWindowClassName(nint hwnd);

    /// <summary>
    /// Gets the extended window style of a window.
    /// </summary>
    int GetWindowExStyle(nint hwnd);

    /// <summary>
    /// Gets the current rectangle (position and size) of a window.
    /// </summary>
    Rectangle GetWindowRect(nint hwnd);

    /// <summary>
    /// Gets the foreground (active) window handle.
    /// </summary>
    nint GetForegroundWindow();
}

/// <summary>
/// Represents a rectangle with integer coordinates.
/// </summary>
public readonly record struct Rectangle(int X, int Y, int Width, int Height);
