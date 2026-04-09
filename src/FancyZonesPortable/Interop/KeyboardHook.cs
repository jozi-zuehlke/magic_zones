using System.Runtime.InteropServices;
using FancyZonesPortable.Core.Abstractions;

namespace FancyZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IKeyboardHook"/> using Win32 SetWindowsHookEx for low-level keyboard hooks.
/// </summary>
internal class KeyboardHook : IKeyboardHook
{
    private const int WM_KEYDOWN = 0x0100;

    private nint _hookHandle;
    // Must hold a reference to prevent GC collection of the delegate
    private NativeMethods.LowLevelKeyboardProc? _callback;

    public event EventHandler<KeyboardHookEventArgs>? KeyPressed;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
            return;

        _callback = HookCallback;
        _hookHandle = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL,
            _callback,
            NativeMethods.GetModuleHandleW(null),
            0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            KeyPressed?.Invoke(this, new KeyboardHookEventArgs { VirtualKeyCode = vkCode });
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Uninstall()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _callback = null;
    }

    public void Dispose()
    {
        Uninstall();
        GC.SuppressFinalize(this);
    }
}
