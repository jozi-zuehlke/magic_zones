namespace MagicZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for low-level keyboard hooks used to detect modifier key
/// state changes and special keys (e.g. Escape) during window drag tracking.
/// </summary>
public interface IKeyboardHook : IDisposable
{
    /// <summary>
    /// Fired when a key is pressed (WM_KEYDOWN / WM_SYSKEYDOWN).
    /// </summary>
    event EventHandler<KeyboardHookEventArgs>? KeyPressed;

    /// <summary>
    /// Fired when a key is released (WM_KEYUP / WM_SYSKEYUP).
    /// </summary>
    event EventHandler<KeyboardHookEventArgs>? KeyReleased;

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
