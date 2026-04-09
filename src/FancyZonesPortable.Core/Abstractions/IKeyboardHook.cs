namespace FancyZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for low-level keyboard hooks, primarily for Escape key detection during drag.
/// </summary>
public interface IKeyboardHook : IDisposable
{
    /// <summary>
    /// Fired when a monitored key is pressed.
    /// </summary>
    event EventHandler<KeyboardHookEventArgs>? KeyPressed;

    /// <summary>
    /// Installs the keyboard hook.
    /// </summary>
    void Install();

    /// <summary>
    /// Uninstalls the keyboard hook.
    /// </summary>
    void Uninstall();
}

/// <summary>
/// Event arguments for keyboard hook events.
/// </summary>
public class KeyboardHookEventArgs : EventArgs
{
    /// <summary>
    /// The virtual key code of the pressed key.
    /// </summary>
    public int VirtualKeyCode { get; init; }
}
