using System.Runtime.InteropServices;
using FancyZonesPortable.Core.Abstractions;

namespace FancyZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IKeyboardHook"/> using Win32 SetWindowsHookEx for low-level keyboard hooks.
/// </summary>
internal class KeyboardHook : IKeyboardHook
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private nint _hookHandle;
    // Must hold a reference to prevent GC collection of the delegate
    private NativeMethods.LowLevelKeyboardProc? _callback;

    public event EventHandler<KeyboardHookEventArgs>? KeyPressed;
    public event EventHandler<KeyboardHookEventArgs>? KeyReleased;

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
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                KeyPressed?.Invoke(this, new KeyboardHookEventArgs { VirtualKeyCode = vkCode });
            else if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
                KeyReleased?.Invoke(this, new KeyboardHookEventArgs { VirtualKeyCode = vkCode });
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
