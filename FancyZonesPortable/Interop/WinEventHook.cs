namespace FancyZonesPortable.Interop;

/// <summary>
/// RAII wrapper for SetWinEventHook. Prevents the delegate from being garbage collected
/// while the hook is active.
/// </summary>
internal sealed class WinEventHook : IDisposable
{
    private IntPtr _hookHandle;
    private readonly NativeMethods.WinEventDelegate _delegate;
    private bool _disposed;

    public WinEventHook(
        uint eventMin, uint eventMax,
        NativeMethods.WinEventDelegate callback,
        uint idProcess = 0, uint idThread = 0)
    {
        // Store delegate to prevent GC collection
        _delegate = callback;
        _hookHandle = NativeMethods.SetWinEventHook(
            eventMin, eventMax, IntPtr.Zero,
            _delegate, idProcess, idThread,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SetWinEventHook failed for events 0x{eventMin:X4}-0x{eventMax:X4}");
    }

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
