namespace FancyZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for Windows event hooks that detect window move/resize events.
/// </summary>
public interface IWinEventHook : IDisposable
{
    /// <summary>
    /// Fired when a window move or resize begins.
    /// </summary>
    event EventHandler<WinEventArgs>? WindowMoveStarted;

    /// <summary>
    /// Fired when a window move or resize ends.
    /// </summary>
    event EventHandler<WinEventArgs>? WindowMoveEnded;

    /// <summary>
    /// Starts listening for window events.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening for window events.
    /// </summary>
    void Stop();
}

/// <summary>
/// Event arguments for window events.
/// </summary>
public class WinEventArgs : EventArgs
{
    /// <summary>
    /// The handle of the window that triggered the event.
    /// </summary>
    public nint WindowHandle { get; init; }
}
