namespace FancyZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for registering and unregistering global hotkeys.
/// </summary>
public interface IHotkeyManager : IDisposable
{
    /// <summary>
    /// Fired when the registered hotkey is pressed.
    /// </summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey with the specified modifier and key.
    /// </summary>
    /// <param name="modifiers">Modifier flags (Alt=1, Ctrl=2, Shift=4, Win=8).</param>
    /// <param name="key">The virtual key code.</param>
    /// <returns>True if registration succeeded.</returns>
    bool Register(int modifiers, int key);

    /// <summary>
    /// Unregisters the currently registered hotkey.
    /// </summary>
    void Unregister();
}
