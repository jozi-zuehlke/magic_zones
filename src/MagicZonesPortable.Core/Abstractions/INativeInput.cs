namespace MagicZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for native input state queries (cursor position, key state).
/// </summary>
public interface INativeInput
{
    /// <summary>
    /// Gets the current cursor position in screen coordinates.
    /// </summary>
    (int X, int Y) GetCursorPos();

    /// <summary>
    /// Returns true if the specified virtual key is currently pressed.
    /// </summary>
    bool IsKeyPressed(int virtualKeyCode);
}
