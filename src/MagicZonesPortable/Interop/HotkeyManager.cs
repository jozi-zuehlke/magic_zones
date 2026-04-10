using MagicZonesPortable.Core.Abstractions;

namespace MagicZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IHotkeyManager"/> using Win32 RegisterHotKey/UnregisterHotKey.
/// </summary>
internal class HotkeyManager : IHotkeyManager
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1;

    private MessageWindow? _window;

    public event EventHandler? HotkeyPressed;

    public bool Register(int modifiers, int key)
    {
        _window ??= new MessageWindow(this);

        return NativeMethods.RegisterHotKey(
            _window.Handle,
            HOTKEY_ID,
            (uint)modifiers,
            (uint)key);
    }

    public void Unregister()
    {
        if (_window != null)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, HOTKEY_ID);
            _window.DestroyHandle();
            _window = null;
        }
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Hidden message-only window to receive WM_HOTKEY messages.
    /// </summary>
    private sealed class MessageWindow : NativeWindow
    {
        private readonly HotkeyManager _owner;

        public MessageWindow(HotkeyManager owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                _owner.HotkeyPressed?.Invoke(_owner, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }
    }
}
