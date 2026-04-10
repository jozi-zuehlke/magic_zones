/// Registers and manages a thread-level global hotkey (e.g. `Ctrl+Win+Z`)
/// to toggle snapping on/off.
///
/// # Architecture
///
/// Uses `RegisterHotKey` with `HWND(0)` (no window) so the hotkey is
/// delivered as `WM_HOTKEY` messages to the registering thread's message
/// queue. The engine's message pump picks these up.
///
/// # RAII
///
/// [`HotkeyManager`] is an RAII guard: dropping it calls `UnregisterHotKey`.

use windows::Win32::Foundation::HWND;
use windows::Win32::UI::WindowsAndMessaging::{
    RegisterHotKey, UnregisterHotKey, HOT_KEY_MODIFIERS,
};

/// The unique ID we use for our single hotkey registration.
const HOTKEY_ID: i32 = 1;

/// RAII guard for a Win32 thread-level hotkey registration.
///
/// When dropped, the hotkey is unregistered via `UnregisterHotKey`.
pub struct HotkeyManager {
    hotkey_id: i32,
}

impl HotkeyManager {
    /// Register a thread hotkey with the given modifier flags and virtual-key code.
    ///
    /// The `modifiers` parameter uses the Win32 `MOD_*` constants (e.g.
    /// `MOD_CONTROL | MOD_WIN`). The `vk` parameter is a virtual-key code
    /// (e.g. `0x5A` for the 'Z' key).
    ///
    /// # Errors
    ///
    /// Returns an error if `RegisterHotKey` fails (e.g. the hotkey is already
    /// registered by another application).
    pub fn register_thread_hotkey(modifiers: u32, vk: u32) -> Result<Self, String> {
        // SAFETY: HWND(0) registers a thread-level hotkey (no window handle needed).
        let result = unsafe {
            RegisterHotKey(
                Some(HWND(std::ptr::null_mut())),
                HOTKEY_ID,
                HOT_KEY_MODIFIERS(modifiers),
                vk,
            )
        };

        result.map_err(|e| format!("RegisterHotKey failed: {e}"))?;

        tracing::info!(
            "Hotkey registered: modifiers=0x{:X}, vk=0x{:X}, id={}",
            modifiers,
            vk,
            HOTKEY_ID
        );

        Ok(Self {
            hotkey_id: HOTKEY_ID,
        })
    }
}

impl Drop for HotkeyManager {
    fn drop(&mut self) {
        // SAFETY: We registered with HWND(0) and the same hotkey_id.
        let result = unsafe { UnregisterHotKey(Some(HWND(std::ptr::null_mut())), self.hotkey_id) };
        if result.is_ok() {
            tracing::info!("Hotkey unregistered: id={}", self.hotkey_id);
        } else {
            tracing::warn!("UnregisterHotKey failed for id={}", self.hotkey_id);
        }
    }
}
