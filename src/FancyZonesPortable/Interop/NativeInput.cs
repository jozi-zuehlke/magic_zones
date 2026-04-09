using FancyZonesPortable.Core.Abstractions;

namespace FancyZonesPortable.Interop;

/// <summary>
/// Implements <see cref="INativeInput"/> using Win32 P/Invoke calls.
/// </summary>
internal class NativeInput : INativeInput
{
    public (int X, int Y) GetCursorPos()
    {
        NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
        return (pt.X, pt.Y);
    }

    public bool IsKeyPressed(int virtualKeyCode)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
    }
}
