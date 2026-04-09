using FancyZonesPortable.Core.Abstractions;

namespace FancyZonesPortable.Interop;

/// <summary>
/// RAII wrapper for Win32 SetWinEventHook, implementing <see cref="IWinEventHook"/>.
/// </summary>
internal class WinEventHook : IWinEventHook
{
    private nint _hookHandle;
    // Must hold a reference to prevent GC collection of the delegate
    private NativeMethods.WinEventDelegate? _delegate;

    public event EventHandler<WinEventArgs>? WindowMoveStarted;
    public event EventHandler<WinEventArgs>? WindowMoveEnded;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
            return;

        _delegate = OnEventReceived;
        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero,
            _delegate,
            0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void OnEventReceived(nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_MOVESIZESTART)
            WindowMoveStarted?.Invoke(this, new WinEventArgs { WindowHandle = hwnd });
        else if (eventType == NativeMethods.EVENT_SYSTEM_MOVESIZEEND)
            WindowMoveEnded?.Invoke(this, new WinEventArgs { WindowHandle = hwnd });
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _delegate = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
