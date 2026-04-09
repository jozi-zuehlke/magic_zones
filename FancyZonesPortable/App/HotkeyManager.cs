using FancyZonesPortable.Interop;
using FancyZonesPortable.Logging;

namespace FancyZonesPortable.App;

/// <summary>
/// Wraps RegisterHotKey / UnregisterHotKey for a global toggle hotkey.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 0x1001;
    private readonly IntPtr _hwnd;
    private bool _registered;
    private bool _disposed;

    public event Action? HotkeyPressed;

    public HotkeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>
    /// Registers the hotkey from a string like "Ctrl+Win+Z".
    /// Returns true if registration succeeded.
    /// </summary>
    public bool Register(string hotkeyString)
    {
        Unregister();

        if (!ParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            Logger.Warn($"Could not parse hotkey string: '{hotkeyString}'");
            return false;
        }

        modifiers |= NativeMethods.MOD_NOREPEAT;

        _registered = NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, vk);

        if (_registered)
            Logger.Info($"Registered global hotkey: {hotkeyString}");
        else
            Logger.Warn($"Failed to register hotkey '{hotkeyString}' — it may be in use by another application.");

        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }

    /// <summary>
    /// Call this from WndProc when WM_HOTKEY is received.
    /// </summary>
    public void HandleWmHotkey(int id)
    {
        if (id == HOTKEY_ID)
            HotkeyPressed?.Invoke();
    }

    private static bool ParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts[..^1])
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    Logger.Warn($"Unknown hotkey modifier: '{part}'");
                    return false;
            }
        }

        var keyName = parts[^1].ToUpperInvariant();
        if (keyName.Length == 1 && char.IsLetterOrDigit(keyName[0]))
        {
            vk = (uint)keyName[0]; // ASCII = virtual key for A-Z, 0-9
        }
        else
        {
            // Try to map common key names
            vk = keyName switch
            {
                "F1" => 0x70u, "F2" => 0x71u, "F3" => 0x72u, "F4" => 0x73u,
                "F5" => 0x74u, "F6" => 0x75u, "F7" => 0x76u, "F8" => 0x77u,
                "F9" => 0x78u, "F10" => 0x79u, "F11" => 0x7Au, "F12" => 0x7Bu,
                "SPACE" => 0x20u, "ENTER" => 0x0Du, "TAB" => 0x09u,
                "ESCAPE" or "ESC" => 0x1Bu,
                _ => 0u
            };
        }

        return vk != 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
    }
}
