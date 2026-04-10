/// Installs a low-level keyboard hook (`WH_KEYBOARD_LL`) to detect key presses globally.
///
/// # Architecture
///
/// Similar to [`super::win_event_hook`], the hook callback has a C ABI so we
/// store the [`Sender`] in a global [`OnceLock`].
///
/// The callback **always** calls `CallNextHookEx` to ensure other hooks in the
/// chain are not disrupted. This is a hard requirement for low-level hooks.
///
/// # RAII
///
/// [`KeyboardHook`] is an RAII guard: dropping it calls `UnhookWindowsHookEx`.
///
/// # Panics / Safety
///
/// The callback is wrapped in [`std::panic::catch_unwind`] so a panic will not
/// unwind across the FFI boundary.

use std::sync::OnceLock;

use crossbeam_channel::Sender;

use crate::hooks::KeyboardMsg;

use windows::Win32::Foundation::{LPARAM, LRESULT, WPARAM};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, SetWindowsHookExW, UnhookWindowsHookEx, HHOOK, KBDLLHOOKSTRUCT,
    WH_KEYBOARD_LL, WM_KEYDOWN, WM_KEYUP, WM_SYSKEYDOWN, WM_SYSKEYUP,
};

/// Global sender for the keyboard callback. Set once during [`KeyboardHook::install`].
static KBD_HOOK_TX: OnceLock<Sender<KeyboardMsg>> = OnceLock::new();

/// RAII guard for a Win32 low-level keyboard hook.
///
/// When dropped, the hook is uninstalled via `UnhookWindowsHookEx`.
pub struct KeyboardHook {
    hook: HHOOK,
}

impl KeyboardHook {
    /// Install the low-level keyboard hook and begin sending [`KeyboardMsg`]
    /// events to `sender`.
    ///
    /// # Errors
    ///
    /// Returns an error if:
    /// - The global sender has already been set (double-install).
    /// - `SetWindowsHookExW` returns a null handle.
    pub fn install(sender: Sender<KeyboardMsg>) -> Result<Self, String> {
        KBD_HOOK_TX
            .set(sender)
            .map_err(|_| "KeyboardHook: global sender already initialised".to_string())?;

        // SAFETY: GetModuleHandleW(None) returns the current executable's HMODULE.
        // SetWindowsHookExW with thread_id 0 installs a global hook.
        let hook = unsafe {
            let hmod = GetModuleHandleW(None)
                .map_err(|e| format!("GetModuleHandleW failed: {e}"))?;

            SetWindowsHookExW(WH_KEYBOARD_LL, Some(keyboard_proc), Some(hmod.into()), 0)
                .map_err(|e| format!("SetWindowsHookExW failed: {e}"))?
        };

        tracing::info!("KeyboardHook installed");
        Ok(Self { hook })
    }
}

impl Drop for KeyboardHook {
    fn drop(&mut self) {
        // SAFETY: `self.hook` was returned by a successful `SetWindowsHookExW` call.
        let ok = unsafe { UnhookWindowsHookEx(self.hook) };
        if ok.is_ok() {
            tracing::info!("KeyboardHook uninstalled");
        } else {
            tracing::warn!("UnhookWindowsHookEx failed");
        }
    }
}

/// Raw Win32 low-level keyboard callback.
///
/// Sends [`KeyboardMsg::KeyDown`] or [`KeyboardMsg::KeyUp`] to the global
/// channel, then **always** calls `CallNextHookEx` to propagate the event.
///
/// Wrapped in `catch_unwind` to prevent panics from unwinding across the FFI
/// boundary. If `catch_unwind` catches a panic we still call `CallNextHookEx`.
extern "system" fn keyboard_proc(code: i32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    let result = std::panic::catch_unwind(|| {
        // Only process events when code >= 0 (per Win32 docs).
        if code >= 0 {
            // SAFETY: lparam points to a KBDLLHOOKSTRUCT for WH_KEYBOARD_LL.
            let kbd = unsafe { &*(lparam.0 as *const KBDLLHOOKSTRUCT) };
            let vk_code = kbd.vkCode;

            let msg = match wparam.0 as u32 {
                WM_KEYDOWN | WM_SYSKEYDOWN => Some(KeyboardMsg::KeyDown(vk_code)),
                WM_KEYUP | WM_SYSKEYUP => Some(KeyboardMsg::KeyUp(vk_code)),
                _ => None,
            };

            if let Some(msg) = msg {
                if let Some(tx) = KBD_HOOK_TX.get() {
                    let _ = tx.send(msg);
                }
            }
        }
    });

    if result.is_err() {
        tracing::error!("keyboard_proc: panic caught in hook callback");
    }

    // CRITICAL: Always call the next hook in the chain.
    // SAFETY: passing our hook handle and the original parameters.
    unsafe { CallNextHookEx(None, code, wparam, lparam) }
}
